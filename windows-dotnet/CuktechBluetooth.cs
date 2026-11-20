using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Storage.Streams;
using System.Threading.Channels;
namespace Charker;
internal sealed class CuktechSecrets {
 readonly Dictionary<string,string> tokens;readonly object gate=new();
 public CuktechSecrets(){try{var b=ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(Store.Root,"cuktech-tokens.bin")),null,DataProtectionScope.CurrentUser);try{tokens=JsonSerializer.Deserialize<Dictionary<string,string>>(b)??[];}finally{CryptographicOperations.ZeroMemory(b);}}catch{tokens=[];}}
 public string? Get(string mac){lock(gate)return tokens.GetValueOrDefault(Key(mac));}
 static string Key(string mac)=>CuktechProtocol.Mac(CuktechProtocol.Address(mac));
 public void Put(string mac,string token){var b=CuktechProtocol.Token(token);lock(gate){tokens[Key(mac)]=Convert.ToHexString(b);Save();}CryptographicOperations.ZeroMemory(b);}
 public void Remove(string mac){lock(gate){if(tokens.Remove(Key(mac)))Save();}}
 void Save(){Directory.CreateDirectory(Store.Root);byte[] b=JsonSerializer.SerializeToUtf8Bytes(tokens);try{var encrypted=ProtectedData.Protect(b,null,DataProtectionScope.CurrentUser);var path=Path.Combine(Store.Root,"cuktech-tokens.bin");File.WriteAllBytes(path+".tmp",encrypted);File.Move(path+".tmp",path,true);}finally{CryptographicOperations.ZeroMemory(b);}}
}
internal sealed class CuktechBluetooth : IDisposable {
 static Guid Uuid(ushort n)=>Guid.Parse($"{n:x8}-0000-1000-8000-00805f9b34fb");
 readonly object discoveryGate=new();readonly Dictionary<ulong,(BleCandidate Device,DateTimeOffset Seen)> candidates=[];readonly System.Collections.Concurrent.ConcurrentDictionary<ushort,GattCharacteristic> chars=[];readonly System.Collections.Concurrent.ConcurrentDictionary<ushort,Channel<byte[]>> queues=[];readonly SemaphoreSlim writes=new(1),portChanges=new(1);
 BluetoothLEAdvertisementWatcher? watcher;BluetoothLEDevice? device;GattDeviceService? service;GattSession? gatt;CancellationTokenSource? session;MiBleSession? mi;Task release=Task.CompletedTask;
 public DeviceState State {get;private set;}=DeviceState.Empty(CuktechProtocol.Model);public event Action? Changed;public event Action<string>? Error;public event Action<string>? Trace;public string Phase {get;private set;}="尚未连接";
 void Stage(string text){Phase=text;Trace?.Invoke("阶段："+text);State.Status=text;Changed?.Invoke();}
 public BleCandidate[] Candidates {get{lock(discoveryGate)return candidates.Values.Where(x=>DateTimeOffset.UtcNow-x.Seen<TimeSpan.FromSeconds(15)).Select(x=>x.Device).OrderByDescending(x=>x.Rssi).ToArray();}}
 static byte[] Bytes(IBuffer buffer){using var r=DataReader.FromBuffer(buffer);var b=new byte[r.UnconsumedBufferLength];r.ReadBytes(b);return b;}
 public async Task Scan(){StopScan();lock(discoveryGate)candidates.Clear();var radios=await Radio.GetRadiosAsync();if(!radios.Any(r=>r.Kind==RadioKind.Bluetooth&&r.State==RadioState.On))throw new IOException("请先开启 Windows 蓝牙");watcher=new(){ScanningMode=BluetoothLEScanningMode.Active};watcher.Received+=(_,e)=>{if(!e.IsConnectable&&e.AdvertisementType!=BluetoothLEAdvertisementType.ConnectableUndirected)return;var a=e.Advertisement;if(!CuktechProtocol.Advertisement(a.LocalName,a.DataSections.Where(x=>x.DataType==0x16).Select(x=>Bytes(x.Data))))return;lock(discoveryGate)candidates[e.BluetoothAddress]=(new(e.BluetoothAddress,"酷态科 10 Ultra",e.RawSignalStrengthInDBm),DateTimeOffset.UtcNow);Changed?.Invoke();};watcher.Stopped+=(_,e)=>{if(e.Error!=BluetoothError.Success)Error?.Invoke("酷态科扫描停止："+e.Error);};watcher.Start();}
 public void StopScan(){if(watcher==null)return;if(watcher.Status==BluetoothLEAdvertisementWatcherStatus.Started)watcher.Stop();watcher=null;}
 async Task<int> Rediscover(ulong address,CancellationToken ct,bool waitForRelease=false){
  Stage(waitForRelease?"等待上一条蓝牙连接释放":"重新发现已保存的酷态科设备");Trace?.Invoke(waitForRelease?"旧连接仍处于连接态，等待目标重新广播后再认证":"系统缓存未命中，启动目标地址发现；不记录蓝牙地址");
  var found=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
  var refresh=new BluetoothLEAdvertisementWatcher{ScanningMode=BluetoothLEScanningMode.Active};
  void Seen(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementReceivedEventArgs e){
   if(e.BluetoothAddress==address&&(e.IsConnectable||e.AdvertisementType==BluetoothLEAdvertisementType.ConnectableUndirected))found.TrySetResult((int)e.BluetoothAddressType);
  }
  void Stopped(BluetoothLEAdvertisementWatcher sender,BluetoothLEAdvertisementWatcherStoppedEventArgs e){if(e.Error!=BluetoothError.Success)found.TrySetException(new IOException("酷态科蓝牙发现失败："+e.Error));}
  refresh.Received+=Seen;refresh.Stopped+=Stopped;
  try{refresh.Start();var type=await found.Task.WaitAsync(TimeSpan.FromSeconds(waitForRelease?20:12),ct);Trace?.Invoke("已重新发现目标，地址类型 "+(BluetoothAddressType)type);return type;}
  catch(TimeoutException){throw new IOException(waitForRelease?"上一条酷态科蓝牙连接尚未释放，请退出其他连接这台设备的应用后重试。":"未发现已保存的酷态科，请确认充电器已通电、处于蓝牙范围内，并退出手机米家设备页面后重试。");}
  finally{refresh.Received-=Seen;refresh.Stopped-=Stopped;if(refresh.Status==BluetoothLEAdvertisementWatcherStatus.Started)refresh.Stop();}
 }
 async Task<BluetoothLEDevice> OpenDevice(ulong address,CancellationToken ct){
  var radios=await Radio.GetRadiosAsync().AsTask(ct);
  if(!radios.Any(r=>r.Kind==RadioKind.Bluetooth))throw new IOException("未检测到 Windows 蓝牙适配器。");
  if(!radios.Any(r=>r.Kind==RadioKind.Bluetooth&&r.State==RadioState.On))throw new IOException("请先开启 Windows 蓝牙，再连接酷态科。");
  bool waitingForRelease=false;
  return await CuktechDeviceOpening.Resolve(
   async cancel=>await BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(cancel).WaitAsync(TimeSpan.FromSeconds(20),cancel),
   cancel=>Rediscover(address,cancel,waitingForRelease),
   async(type,cancel)=>{Stage("打开已发现的酷态科设备");return await BluetoothLEDevice.FromBluetoothAddressAsync(address,(BluetoothAddressType)type).AsTask(cancel).WaitAsync(TimeSpan.FromSeconds(20),cancel);},ct,requiresRediscovery:d=>{waitingForRelease=d.ConnectionStatus==BluetoothConnectionStatus.Connected;return waitingForRelease;},releaseCached:d=>d.Dispose());
 }
 async Task Subscribe(ushort id,CancellationToken ct){var c=chars[id];Trace?.Invoke($"订阅通知 0x{id:X4}，属性 {c.CharacteristicProperties}");c.ValueChanged+=Received;var mode=c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify)?GattClientCharacteristicConfigurationDescriptorValue.Notify:GattClientCharacteristicConfigurationDescriptorValue.Indicate;if(await c.WriteClientCharacteristicConfigurationDescriptorAsync(mode).AsTask(ct)!=GattCommunicationStatus.Success)throw new IOException("无法订阅酷态科蓝牙通知");}
 void Received(GattCharacteristic sender,GattValueChangedEventArgs e){ushort id=(ushort)uint.Parse(sender.Uuid.ToString()[..8],System.Globalization.NumberStyles.HexNumber);Trace?.Invoke($"RX 0x{id:X4} 长度 {e.CharacteristicValue.Length}，对象匹配 {chars.TryGetValue(id,out var existing)&&ReferenceEquals(sender,existing)}");var received=Bytes(e.CharacteristicValue);if(id==0x10&&received.Length==4)Trace?.Invoke($"认证控制状态 0x{received[0]:X2}");if(chars.TryGetValue(id,out var current)&&ReferenceEquals(sender,current)&&queues.TryGetValue(id,out var q)&&!q.Writer.TryWrite(received))Failure(new IOException("酷态科蓝牙通知队列已满，请重新连接"));}
 async Task Write(ushort id,byte[] b,CancellationToken ct){await writes.WaitAsync(ct);try{if(!chars.TryGetValue(id,out var c))throw new IOException("酷态科蓝牙已断开");Trace?.Invoke($"TX 0x{id:X4} 长度 {b.Length}");using var w=new DataWriter();w.WriteBytes(b);var option=c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse)?GattWriteOption.WriteWithoutResponse:GattWriteOption.WriteWithResponse;var r=await c.WriteValueWithResultAsync(w.DetachBuffer(),option).AsTask(ct).WaitAsync(TimeSpan.FromSeconds(8),ct);if(r.Status!=GattCommunicationStatus.Success)throw new IOException("酷态科蓝牙写入失败："+r.Status);}finally{writes.Release();}}
 void Failure(Exception e){if(session?.IsCancellationRequested!=false)return;session.Cancel();State.Connected=false;State.Status="酷态科蓝牙已断开，请手动连接";Error?.Invoke(e.Message);Changed?.Invoke();}
 void Disconnected(BluetoothLEDevice sender,object args){if(ReferenceEquals(sender,device)&&State.Connected&&sender.ConnectionStatus==BluetoothConnectionStatus.Disconnected)Failure(new IOException("酷态科蓝牙连接中断"));}
 public async Task Connect(ulong address,string token,CancellationToken cancellation=default){Disconnect();StopScan();var own=new CancellationTokenSource(TimeSpan.FromSeconds(90));using var registration=cancellation.Register(()=>own.Cancel());session=own;var ct=own.Token;State=DeviceState.Empty(CuktechProtocol.Model,address.ToString());State.HardwareAddress=CuktechProtocol.Mac(address);State.Status="正在连接酷态科蓝牙…";Changed?.Invoke();byte[] secret=CuktechProtocol.Token(token);
  try{await release.WaitAsync(ct);Stage("打开 Windows 蓝牙设备");device=await OpenDevice(address,ct);device.ConnectionStatusChanged+=Disconnected;Stage("建立 GATT 会话");gatt=await GattSession.FromDeviceIdAsync(device.BluetoothDeviceId).AsTask(ct);if(gatt!=null)gatt.MaintainConnection=true;Stage("发现 Mi BLE 服务");var sr=await device.GetGattServicesForUuidAsync(Uuid(0xfe95),BluetoothCacheMode.Uncached).AsTask(ct);if(sr.Status!=GattCommunicationStatus.Success||sr.Services.Count==0)throw new IOException("找不到酷态科 Mi BLE 服务");service=sr.Services[0];Stage("发现 GATT 特征");var cr=await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(ct);if(cr.Status!=GattCommunicationStatus.Success)throw new IOException("无法读取酷态科 GATT 特征");foreach(var id in new ushort[]{0x10,0x19,0x1a,0x1b}){chars[id]=cr.Characteristics.FirstOrDefault(c=>c.Uuid==Uuid(id))??throw new IOException("酷态科蓝牙特征不完整");queues[id]=Channel.CreateBounded<byte[]>(64);}Trace?.Invoke($"协商 MTU {gatt?.MaxPduSize??23}");Stage("读取固件版本");var fw=cr.Characteristics.FirstOrDefault(c=>c.Uuid==Uuid(4));if(fw!=null){var r=await fw.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(ct);if(r.Status==GattCommunicationStatus.Success)State.Firmware=Encoding.ASCII.GetString(Bytes(r.Value)).Trim('\0');}
   Stage("订阅蓝牙通知");if(await chars[0x10].WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None).AsTask(ct)!=GattCommunicationStatus.Success)throw new IOException("无法复位酷态科认证通知，请稍后重新连接");await Subscribe(0x1a,ct);await Subscribe(0x1b,ct);await Subscribe(0x19,ct);mi=new(Write,(id,cancel)=>queues[id].Reader.ReadAsync(cancel).AsTask(),()=>gatt?.MaxPduSize??23,ct);mi.Progress+=Stage;mi.Trace+=text=>Trace?.Invoke(text);mi.Properties+=properties=>{if(!ReferenceEquals(session,own)||ct.IsCancellationRequested)return;CuktechProtocol.Apply(State,properties);Changed?.Invoke();};mi.Failed+=e=>{if(ReferenceEquals(session,own))Failure(e);};State.Status="正在验证酷态科设备…";Changed?.Invoke();await mi.Login(secret,()=>Subscribe(0x10,ct));Stage("读取端口和设置");await mi.ReadProperties(1,2,3,4,5,6,7,13,15,16,19,20);ct.ThrowIfCancellationRequested();State.Connected=true;Phase="已连接";State.Status="已连接";own.CancelAfter(Timeout.InfiniteTimeSpan);Changed?.Invoke();_=Poll(ct);
  }catch(Exception e){string detail=$"酷态科连接失败（{Phase}）：{(e is TimeoutException?"等待设备确认超时":e.Message)}";Disconnect();State.Status=detail;Error?.Invoke(detail);Changed?.Invoke();throw new IOException(detail,e);}finally{CryptographicOperations.ZeroMemory(secret);}}
 async Task Poll(CancellationToken ct){int n=0;try{while(!ct.IsCancellationRequested){await Task.Delay(1000,ct);if(mi==null||!State.Connected)continue;if(n++%15==0)await mi.ReadProperties(1,2,3,4,5,6,7,13,15,16,19,20);else if(State.Updated==null||DateTimeOffset.UtcNow-State.Updated>TimeSpan.FromMilliseconds(900))await mi.ReadProperties(1,2,3,4);}}catch(OperationCanceledException){}catch(Exception e){if(!ct.IsCancellationRequested)Failure(e);}}
 public async Task SetPort(int index,bool on){if(index<0||index>3)throw new ArgumentException("无效酷态科端口");await portChanges.WaitAsync();try{int mask;lock(State.Sync){if(!State.Settings.TryGetValue("portMask",out var raw))throw new IOException("尚未读取端口开关状态，请稍后再试");mask=Convert.ToInt32(raw);}await Set(16,on?mask|(1<<index):mask&~(1<<index));}finally{portChanges.Release();}}
 public Task Set(int id,int value){if(!State.Connected||mi==null)throw new IOException("请先连接酷态科");bool valid=id switch{5=>value is >=1 and <=4,6=>value is >=0 and <=4,13=>value is 0 or 1,15 or 19 or 20=>value is 0 or 1,16=>value is >=0 and <=15,_=>false};if(!valid)throw new ArgumentException("不支持的酷态科设置或参数");return mi.SetProperty(id,value,boolean:id is 15 or 19 or 20);}
 async Task ReleaseTransport(Task previous,GattCharacteristic[] oldChars,GattSession? oldGatt,GattDeviceService? oldService,BluetoothLEDevice? oldDevice){
  await previous.ConfigureAwait(false);
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));
  try{
   // Clear this client's CCCDs before releasing GATT, including auth_ctrl.
   // Leaving subscriptions behind can poison a rapid standard-auth reconnect.
   foreach(var c in oldChars){if(timeout.IsCancellationRequested)break;await c.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None).AsTask(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);}
   Trace?.Invoke("已注销上一会话的酷态科通知");
  }catch(Exception e){Trace?.Invoke("释放上一会话通知："+(e is OperationCanceledException?"达到清理时限":e.GetType().Name));}
  finally{
   try{if(oldGatt!=null)oldGatt.MaintainConnection=false;}catch(Exception){}
   try{oldGatt?.Dispose();}catch(Exception){}try{oldService?.Dispose();}catch(Exception){}try{oldDevice?.Dispose();}catch(Exception){}
  }
 }
 public void Disconnect(){
  var own=session;session=null;own?.Cancel();mi?.Dispose();mi=null;
  var oldChars=chars.Values.ToArray();foreach(var c in oldChars)c.ValueChanged-=Received;chars.Clear();queues.Clear();
  var oldDevice=device;var oldGatt=gatt;var oldService=service;device=null;gatt=null;service=null;
  if(oldDevice!=null)oldDevice.ConnectionStatusChanged-=Disconnected;
  State.Connected=false;own?.Dispose();
  if(oldChars.Length>0||oldDevice!=null||oldGatt!=null||oldService!=null)release=ReleaseTransport(release,oldChars,oldGatt,oldService,oldDevice);
 }
 public void Dispose(){StopScan();Disconnect();try{release.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();}catch(TimeoutException){Trace?.Invoke("蓝牙释放已达到退出时限");}}

}
