using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
namespace Charker;
public record BleCandidate(ulong Address,string Name,int Rssi);
public sealed class BluetoothLink : IDisposable {
 static readonly Guid Service=Guid.Parse("8c850001-0302-41c5-b46e-cf057c562025"),WriteId=Guid.Parse("8c850002-0302-41c5-b46e-cf057c562025"),NotifyId=Guid.Parse("8c850003-0302-41c5-b46e-cf057c562025");
 BluetoothLEAdvertisementWatcher? watcher;BluetoothLEDevice? device;GattDeviceService? service;GattCharacteristic? write,notify;GattSession? gatt;
 readonly BleDiscovery discovery=new();readonly SemaphoreSlim writes=new(1),commands=new(1);
 readonly object receiveGate=new();Reassembler reassembler=new();SessionCrypto? crypto;TaskCompletionSource<Reply>? awaiting;ushort awaited;
 CancellationTokenSource? session;bool uploading;string ownerId="",countryCode="CN";public DeviceState State {get;private set;}=DeviceState.Empty("A2687");public event Action? Changed;public event Action<string>? Error;
 public IReadOnlyCollection<BleCandidate> Candidates=>discovery.Current();
 public void KnownDevices(IEnumerable<SavedDevice> saved,IEnumerable<SavedDevice>? excluded=null)=>discovery.Known(saved,excluded);
 public async Task Scan(){StopScan();discovery.Clear();var radios=await Radio.GetRadiosAsync();if(!radios.Any(r=>r.Kind==RadioKind.Bluetooth&&r.State==RadioState.On))throw new InvalidOperationException("请先开启 Windows 蓝牙，并确认电脑有支持 BLE 的蓝牙适配器。");
  watcher=new(){ScanningMode=BluetoothLEScanningMode.Active};watcher.Received+=(_,e)=>{
   var a=e.Advertisement;bool match=A2687Model.Advertisement(a.LocalName,a.ManufacturerData.Select(m=>Bytes(m.Data)).Concat(a.DataSections.Where(s=>s.DataType==0x16).Select(s=>Bytes(s.Data))));
   if((match||discovery.Contains(e.BluetoothAddress)||discovery.Approved(e.BluetoothAddress))&&discovery.Observe(e.BluetoothAddress,a.LocalName,e.RawSignalStrengthInDBm,e.IsConnectable||e.AdvertisementType is BluetoothLEAdvertisementType.ConnectableUndirected or BluetoothLEAdvertisementType.ConnectableDirected,e.AdvertisementType==BluetoothLEAdvertisementType.ScanResponse,modelMatch:match))Changed?.Invoke();};
  watcher.Stopped+=(_,e)=>{if(e.Error!=BluetoothError.Success)Error?.Invoke("蓝牙扫描停止："+e.Error);};watcher.Start();
 }
 static byte[] Bytes(IBuffer buffer){using var r=DataReader.FromBuffer(buffer);var bytes=new byte[r.UnconsumedBufferLength];r.ReadBytes(bytes);return bytes;}
 static bool HasService(IBuffer buffer){using var r=DataReader.FromBuffer(buffer);return r.UnconsumedBufferLength>=2&&r.ReadByte()==9&&r.ReadByte()==255;}
 public void StopScan(){if(watcher!=null){if(watcher.Status==BluetoothLEAdvertisementWatcherStatus.Started)watcher.Stop();watcher=null;}}
 public async Task Connect(ulong address,string owner,string country,CancellationToken cancellation=default) {
  DisposeConnection();StopScan();ownerId=owner;countryCode=country;session=new();var connectingSession=session;using var registration=cancellation.Register(()=>connectingSession.Cancel());var ct=session.Token;State=DeviceState.Empty("A2687",address.ToString());State.Status="正在连接蓝牙…";Changed?.Invoke();
  try{
   await A2687Connection.Open(
    cancel=>OpenTransport(address,null,cancel),
    async cancel=>await OpenTransport(address,await Rediscover(address,cancel),cancel),
    ReleaseTransport,
    failure=>Error?.Invoke("160W 直接连接失败，正在重新扫描原设备："+failure.Message),ct);
   var r=await Request(3,Wire.Tlv([Wire.F(0xa1,Wire.Epoch()),Wire.F(0xa3,0x20),Wire.F(0xa4,0,0xf0)]),1,ct);
   var mtu=r.Get(0xa2)??[0x29,1];var auth=r.Get(0xa3)?.FirstOrDefault()??0x44;var enc=r.Get(0xa5)?.FirstOrDefault()??2;
   r=await Request(0x29,Wire.Tlv([Wire.F(0xa1,Wire.Epoch())]),1,ct);
   string info=Encoding.UTF8.GetString(r.Get(0xa2)??[]).Trim('\0','.');if(info.Length>0)State.Name=info;State.Firmware=Encoding.UTF8.GetString(r.Get(0xa3)??[]).Trim('\0','.');var identity=DeviceRegistry.Identity(r);State.SerialNumber=identity.Serial;State.HardwareAddress=identity.Mac;if(!A2687Model.ChargerSerial(State.SerialNumber))throw new IOException("这台蓝牙设备不是受支持的 Prime 160W 充电器，已停止连接。");
   await Request(5,Wire.Tlv([Wire.F(0xa1,Wire.Epoch()),Wire.F(0xa3,0x20),Wire.F(0xa4,mtu),Wire.F(0xa5,auth),Wire.F(0xa6,enc)]),1,ct);
   using(var own=ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)) {var q=own.ExportParameters(false).Q;r=await Request(0x21,Wire.Tlv([Wire.F(0xa1,q.X!.Concat(q.Y!).ToArray())]),1,ct);crypto.Derive(own,r.Get(0xa1)??[]);}
   try{await Request(0x22,Wire.Tlv([Wire.F(0xa1,Wire.Epoch()),Wire.F(0xa3,0x80,0x8f,0xff,0xff),Wire.F(0xa5,Encoding.UTF8.GetBytes(TimeZoneRule()))]),1,ct);}catch(TimeoutException){}
   try{await Request(0x27,Wire.Tlv([Wire.F(0xa1,Wire.Epoch()),Wire.F(0xa2,Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(owner)?"CharkerWindows":owner))]),1,ct);}catch(TimeoutException){}
   State.Connected=true;State.Status="已连接，等待功率数据…";Changed?.Invoke();
   await Send(0x200,Wire.Session(),15,ct);await Task.Delay(400,ct);await Probe(ct);await Task.Delay(400,ct);await Trigger(ct);
   _=Poll(ct);
  } catch(Exception e){
   DisposeConnection();State.Status="连接失败";
   if(e is not OperationCanceledException)Error?.Invoke("160W 连接失败："+e.Message);
   Changed?.Invoke();throw;
  }
 }
 async Task<BluetoothAddressType> Rediscover(ulong address,CancellationToken ct){
  State.Status="正在重新扫描已保存的 160W…";Changed?.Invoke();
  var radios=await Radio.GetRadiosAsync().AsTask(ct).WaitAsync(TimeSpan.FromSeconds(5),ct);
  if(!radios.Any(r=>r.Kind==RadioKind.Bluetooth&&r.State==RadioState.On))
   throw new IOException("Windows 蓝牙未开启或适配器暂不可用。");
  var found=new TaskCompletionSource<BluetoothAddressType>(TaskCreationOptions.RunContinuationsAsynchronously);
  var scan=new BluetoothLEAdvertisementWatcher{ScanningMode=BluetoothLEScanningMode.Active};
  void ReceivedAdvertisement(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementReceivedEventArgs e){
   if(e.BluetoothAddress==address&&(e.IsConnectable||e.AdvertisementType is BluetoothLEAdvertisementType.ConnectableUndirected or BluetoothLEAdvertisementType.ConnectableDirected))
    found.TrySetResult(e.BluetoothAddressType);
  }
  void Stopped(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementWatcherStoppedEventArgs e){
   if(e.Error!=BluetoothError.Success)found.TrySetException(new IOException("Windows 蓝牙扫描中止："+e.Error));
  }
  scan.Received+=ReceivedAdvertisement;
  scan.Stopped+=Stopped;
  try{
   scan.Start();
   return await found.Task.WaitAsync(TimeSpan.FromSeconds(12),ct);
  }catch(TimeoutException){
   throw new IOException("12 秒内没有发现原来保存的 160W。请确认充电器已通电并在蓝牙范围内；若扫描显示新地址，请从扫描结果连接。");
  }finally{
   scan.Received-=ReceivedAdvertisement;scan.Stopped-=Stopped;
   if(scan.Status==BluetoothLEAdvertisementWatcherStatus.Started)scan.Stop();
  }
 }
 async Task OpenTransport(ulong address,BluetoothAddressType? addressType,CancellationToken cancellation){
  using var attempt=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
  var ct=attempt.Token;
  try{await OpenGatt(address,addressType,ct);}
  catch{attempt.Cancel();throw;}
 }
 async Task OpenGatt(ulong address,BluetoothAddressType? addressType,CancellationToken ct){
  var lookup=addressType is {} type
   ?BluetoothLEDevice.FromBluetoothAddressAsync(address,type)
   :BluetoothLEDevice.FromBluetoothAddressAsync(address);
  device=await lookup.AsTask(ct).WaitAsync(TimeSpan.FromSeconds(6),ct)
   ??throw new IOException("Windows 未找到已保存的蓝牙地址。");
  device.ConnectionStatusChanged+=ConnectionChanged;
  gatt=await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(8),ct);
  if(gatt!=null)gatt.MaintainConnection=true;
  var services=await device.GetGattServicesForUuidAsync(Service,BluetoothCacheMode.Uncached).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(9),ct);
  if(services.Status!=GattCommunicationStatus.Success||services.Services.Count==0)
   throw new IOException("无法读取 160W 蓝牙服务："+services.Status);
  service=services.Services[0];
  var chars=await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(8),ct);
  if(chars.Status!=GattCommunicationStatus.Success)throw new IOException("无法读取 160W 蓝牙特征："+chars.Status);
  write=chars.Characteristics.FirstOrDefault(c=>c.Uuid==WriteId);
  notify=chars.Characteristics.FirstOrDefault(c=>c.Uuid==NotifyId);
  if(write==null||notify==null)throw new IOException("充电器蓝牙特征不完整。");
  var mode=notify.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)
   ?GattClientCharacteristicConfigurationDescriptorValue.Notify:GattClientCharacteristicConfigurationDescriptorValue.Indicate;
  crypto=new();notify.ValueChanged+=Received;
  if(await notify.WriteClientCharacteristicConfigurationDescriptorAsync(mode).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(8),ct)!=GattCommunicationStatus.Success)
   throw new IOException("无法订阅 160W 蓝牙通知。");
  State.Status="正在完成加密握手…";Changed?.Invoke();
  await Request(1,Wire.Tlv([Wire.F(0xa1,Wire.Epoch())]),1,ct);
 }
 static string TimeZoneRule(){var off=TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);return off==TimeSpan.FromHours(8)?"CST-8":"GMT"+(off.TotalHours<=0?"+":"-")+Math.Abs(off.TotalHours).ToString(System.Globalization.CultureInfo.InvariantCulture);}
 Task Trigger(CancellationToken ct)=>Send(0x20b,Wire.Session([Wire.Block(0xa2,[1,0,3,0x15,1,1,0,0,0])]),15,ct);
 Task Probe(CancellationToken ct){var fields=new List<Field>{Wire.Block(0xa2,Encoding.UTF8.GetBytes(countryCode)),Wire.U8(0xa5,1)};if(!string.IsNullOrWhiteSpace(ownerId))fields.Add(Wire.Block(0xa3,Encoding.UTF8.GetBytes(ownerId)));return Send(0x20a,Wire.Session(fields),15,ct);}
 async Task Poll(CancellationToken ct){var clock=System.Diagnostics.Stopwatch.StartNew();var schedule=new BlePollSchedule();try{while(!ct.IsCancellationRequested){await Task.Delay(250,ct);if(uploading||commands.CurrentCount==0||!State.Connected)continue;double age=State.Updated is {} at?(DateTimeOffset.UtcNow-at).TotalSeconds:clock.Elapsed.TotalSeconds;var due=schedule.Due(clock.Elapsed.TotalSeconds,age);
   if(due.Probe)await Probe(ct);if(due.Read){if(due.Probe)await Task.Delay(400,ct);if(!uploading)await Send(0x200,Wire.Session(),15,ct);}if(due.Trigger){await Task.Delay(400,ct);if(!uploading)await Trigger(ct);}if(State.Stale){State.Status="已连接，等待充电器上报数据";Changed?.Invoke();}
  }}catch(OperationCanceledException){}catch(Exception e){if(!ct.IsCancellationRequested){State.Connected=false;State.Status="蓝牙连接中断";Error?.Invoke(e.Message);Changed?.Invoke();}}}
 void ConnectionChanged(BluetoothLEDevice sender,object args){if(ReferenceEquals(sender,device)&&sender.ConnectionStatus==BluetoothConnectionStatus.Disconnected&&State.Connected){State.Connected=false;State.Status="蓝牙已断开";awaiting?.TrySetException(new IOException("蓝牙已断开"));Error?.Invoke("160W 蓝牙连接中断：Windows 报告设备已断开。");Changed?.Invoke();}}
 void Received(GattCharacteristic sender,GattValueChangedEventArgs args){try{if(!ReferenceEquals(sender,notify))return;using var reader=DataReader.FromBuffer(args.CharacteristicValue);var bytes=new byte[reader.UnconsumedBufferLength];reader.ReadBytes(bytes);lock(receiveGate){if(!ReferenceEquals(sender,notify))return;foreach(var f in reassembler.Append(bytes)){
   if(f.Slave==0&&(f.Command&0x800)==0)continue;byte[] p=f.Encrypted?crypto!.Open(f.Payload):f.Payload;var r=Wire.Parse(p);
   if(f.Opcode==awaited)awaiting?.TrySetResult(r);
   if(r.Status is >0){if(f.Opcode!=0x21f&&f.Opcode!=0x221)Error?.Invoke($"设备拒绝命令 0x{f.Opcode:X4}，状态 {r.Status}");continue;}
   if(Telemetry.Apply(State,f.Opcode,r))Changed?.Invoke();
  }}}catch(CryptographicException){Error?.Invoke("收到的蓝牙数据认证失败，已丢弃。");}catch(InvalidDataException){}catch(Exception e){Error?.Invoke("蓝牙数据处理失败："+e.GetType().Name);}}
 async Task Send(ushort op,byte[] plaintext,byte group,CancellationToken ct){await writes.WaitAsync(ct);try{if(write==null||crypto==null)throw new IOException("蓝牙未连接");var b=Wire.Encode((ushort)(op|0x4000),crypto.Seal(plaintext),group);int chunk=Math.Max(20,(gatt?.MaxPduSize??23)-3);for(int i=0;i<b.Length;i+=chunk){using var w=new DataWriter();w.WriteBytes(b[i..Math.Min(b.Length,i+chunk)]);var option=write.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Write)?GattWriteOption.WriteWithResponse:GattWriteOption.WriteWithoutResponse;var result=await write.WriteValueWithResultAsync(w.DetachBuffer(),option).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(10),ct);if(result.Status!=GattCommunicationStatus.Success)throw new IOException("蓝牙写入失败："+result.Status);}}finally{writes.Release();}}
 async Task<Reply> Request(ushort op,byte[] p,byte group,CancellationToken ct,bool acceptStatus=false){await commands.WaitAsync(ct);try{awaited=op;awaiting=new(TaskCreationOptions.RunContinuationsAsynchronously);await Send(op,p,group,ct);var r=await awaiting.Task.WaitAsync(TimeSpan.FromSeconds(7),ct);if(r.Status is >0&&!acceptStatus)throw new IOException($"设备拒绝命令 0x{op:X4}，状态 {r.Status}。请核对 Anker 账号 ID。");return r;}finally{awaiting=null;commands.Release();}}
 public Task<Reply> Command(ushort op,IEnumerable<Field> fields){if(uploading)throw new IOException("图片正在传输，请稍候。");return Request(op,Wire.Session(fields),15,session?.Token??CancellationToken.None);}
 public async Task UploadCover(byte[] jpeg,uint pictureId,Action<int> progress){if(jpeg.Length==0||jpeg.Length>1024*1024)throw new InvalidDataException("图片大小无效");if(uploading)throw new IOException("已有图片正在传输");uploading=true;try{await UploadCore(jpeg,pictureId,progress);}finally{uploading=false;}}
 async Task UploadCore(byte[] jpeg,uint pictureId,Action<int> progress){var ct=session?.Token??CancellationToken.None;uint crc=Wire.Crc32(jpeg);int count=(jpeg.Length+155)/156;
  await Request(0x21f,Wire.Session([Wire.U8(0xa3,3),Wire.Block(0xa4,BitConverter.GetBytes(pictureId)),Wire.Block(0xa5,BitConverter.GetBytes(crc)),Wire.F(0xfd,new byte[]{0}.Concat(Encoding.UTF8.GetBytes("SmallChargingUrl")).ToArray())],true),15,ct,true);await Task.Delay(150,ct);
  await Request(0x220,Wire.Session([Wire.U8(0xa2,1),Wire.Block(0xa3,BitConverter.GetBytes(pictureId)),Wire.Block(0xa4,BitConverter.GetBytes(crc)),Wire.F(0xa5,new byte[]{3}.Concat(BitConverter.GetBytes((uint)jpeg.Length)).ToArray()),Wire.U8(0xa6,10),Wire.F(0xa7,2,156,0),Wire.F(0xa8,2,(byte)count,(byte)(count>>8))],true),15,ct);
  await Task.Delay(200,ct);for(int i=0;i<count;i++){byte[] b=new byte[156];Array.Copy(jpeg,i*156,b,0,Math.Min(156,jpeg.Length-i*156));var p=Wire.Tlv([Wire.F(0xa1,0x21),Wire.F(0xa2,2,(byte)i,(byte)(i>>8)),Wire.Block(0xa3,b)]);if(i==count-1||(i+1)%10==0){var r=await Request(0x221,p,15,ct,true);if(r.Status is >0 && !(i==count-1&&r.Status==0x10))throw new IOException("图片分片被拒绝，状态 "+r.Status);var ack=r.Body(0xa2);if(ack is {Length:>=2}&&BitConverter.ToUInt16(ack)!=i+1)throw new IOException("图片传输确认序号不匹配");}else {await Send(0x221,p,15,ct);await Task.Delay(40,ct);}progress((i+1)*100/count);}
  await Task.Delay(400,ct);bool verified=false;for(int i=0;i<3;i++){try{var r=await Request(0x200,Wire.Session(),15,ct);var cover=r.Body(0xe1);if(cover is {Length:>=4}&&BitConverter.ToUInt16(cover,2)==(ushort)pictureId){verified=true;break;}}catch(TimeoutException){}await Task.Delay(500,ct);}if(!verified)throw new IOException("图片已传输，但屏幕显示状态尚未确认，请查看充电器屏幕。");
 }
 void ReleaseTransport(){
  if(notify!=null)notify.ValueChanged-=Received;
  if(device!=null)device.ConnectionStatusChanged-=ConnectionChanged;
  lock(receiveGate){
   write=null;notify=null;reassembler=new();crypto?.Dispose();crypto=null;
  }
  if(gatt!=null){
   try{gatt.MaintainConnection=false;}catch(System.Runtime.InteropServices.COMException){}
   gatt.Dispose();gatt=null;
  }
  service?.Dispose();service=null;device?.Dispose();device=null;
 }
 void DisposeConnection(){
  session?.Cancel();session?.Dispose();session=null;awaiting?.TrySetCanceled();
  ReleaseTransport();State.Connected=false;
 }
 public void Dispose(){StopScan();DisposeConnection();}
}
