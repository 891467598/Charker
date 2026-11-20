using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
namespace Charker;

public sealed class DesktopWidget : Window {
 readonly Controller controller;readonly Action openMain;
 readonly TextBlock total=new(),caption=new();readonly StackPanel devices=new();readonly Dictionary<string,double> axisCeilings=[];
 readonly Dictionary<string,Border> cards=[];
 readonly Dictionary<string,(TextBlock Power,TextBlock Status,TextBlock CompactStatus,TextBlock Name,StackPanel Ports,List<System.Windows.Shapes.Path> Lines,List<TextBlock> Values)> rows=[];
 readonly Dictionary<string,(DateTimeOffset At,DeviceState State)> shown=[];
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder text,int max);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 void RestoreOnDesktop(){var name=new System.Text.StringBuilder(128);GetClassName(GetForegroundWindow(),name,128);if(name.ToString() is "Progman" or "WorkerW"){var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;ShowWindow(handle,4);SetWindowPos(handle,IntPtr.Zero,0,0,0,0,0x13);}}
 long paintedRevision=-1;
 bool internalClose,positionReady;readonly Border frame;
 static SolidColorBrush Brush(string hex)=>new((Color)ColorConverter.ConvertFromString(hex));
 static TextBlock Text(string text,double size,string color,bool bold=false)=>new(){Text=text,FontSize=size,Foreground=Brush(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};
 public DesktopWidget(Controller c,Action open){
  controller=c;openMain=open;Title="ANKER · Desktop";Width=326;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;FontFamily=new FontFamily("Segoe UI, Microsoft YaHei");
  var content=new StackPanel{Margin=new Thickness(21,18,21,16)};
  frame=new Border{Background=Brush("#F2121D2C"),BorderBrush=Brush("#354458"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Child=content,Margin=new Thickness(8),Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=3,Opacity=.2}};Content=frame;
  content.Children.Add(Text("⚡  ANKER",11,"#A1B4C9",true));
  var value=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,1,0,0)};total.FontSize=43;total.FontWeight=FontWeights.SemiBold;total.Foreground=Brush("#F0F6FF");total.Text="—";value.Children.Add(total);var unit=Text("W",15,"#8299B2");unit.VerticalAlignment=VerticalAlignment.Bottom;unit.Margin=new Thickness(7,0,0,8);value.Children.Add(unit);content.Children.Add(value);
  caption.FontSize=10;caption.Foreground=Brush("#8299B2");caption.Margin=new Thickness(0,0,0,7);content.Children.Add(caption);content.Children.Add(devices);
  foreach(var (model,name,color) in new[]{("A2687","Prime 160W","#FF6670"),("A2345","Prime 250W","#D9F477"),("AD1204U","10 Ultra","#F2AD79")}){
   var card=new StackPanel{Margin=new Thickness(12,11,12,10)};var box=new Border{Background=Brush("#1C293B"),CornerRadius=new CornerRadius(12),Margin=new Thickness(0,4,0,4),Child=card};devices.Children.Add(box);cards[model]=box;box.Visibility=Visibility.Collapsed;
   var title=new DockPanel();var power=Text("— W",16,color,true);DockPanel.SetDock(power,Dock.Right);title.Children.Add(power);var compactStatus=Text("暂无有效输出",10,"#A1B4C9");compactStatus.VerticalAlignment=VerticalAlignment.Center;compactStatus.Margin=new Thickness(6,0,0,0);DockPanel.SetDock(compactStatus,Dock.Right);title.Children.Add(compactStatus);var deviceName=Text(name,12,"#E5EDF7",true);deviceName.TextTrimming=TextTrimming.CharacterEllipsis;title.Children.Add(deviceName);card.Children.Add(title);
   var status=Text("未连接",10,"#8194AC");status.Margin=new Thickness(0,4,0,0);card.Children.Add(status);
   var ports=new StackPanel{Margin=new Thickness(0,6,0,0)};var lines=new List<System.Windows.Shapes.Path>();var values=new List<TextBlock>();
   var canvas=new Canvas{Height=48,ClipToBounds=true};ports.Children.Add(canvas);var legend=new UniformGrid{Rows=1,Margin=new Thickness(0,7,0,0)};ports.Children.Add(legend);
   string[] colors=["#347FDF","#46BAA6","#DBAB4B","#A68ADE","#E58FA3","#D66D36"];
   for(int i=0;i<(model=="AD1204U"?4:model=="A2687"?3:6);i++){var line=new System.Windows.Shapes.Path{Stroke=Brush(colors[i]),StrokeThickness=1.5,StrokeLineJoin=PenLineJoin.Round};canvas.Children.Add(line);var item=new StackPanel();item.Children.Add(Text("● "+(model=="AD1204U"&&i==3?"A":i<4?"C"+(i+1):"A"+(i-3)),9,colors[i],true));var reading=Text("—",10,"#D5E1EF");item.Children.Add(reading);legend.Children.Add(item);lines.Add(line);values.Add(reading);}
   card.Children.Add(ports);rows[model]=(power,status,compactStatus,deviceName,ports,lines,values);
  }
  frame.MouseLeftButtonDown+=(_,e)=>{if(e.ClickCount==2){openMain();return;}if(controller.Prefs.DesktopWidgetLocked)return;try{DragMove();SavePosition();}catch(InvalidOperationException){}};
  ContextMenuService.SetIsEnabled(frame,false);
  Loaded+=(_,_)=>{RestorePosition();positionReady=true;Refresh();};Closing+=(_,e)=>{if(!internalClose)e.Cancel=true;};
 }
 public void ResetPosition(){PlaceDefault();SavePosition();}
 static bool HasButton(DependencyObject node)=>node is Button||LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>().Any(HasButton);
 public object Inspect()=>new{compactCards=cards.Keys.Where(k=>cards[k].IsVisible&&rows[k].CompactStatus.IsVisible&&!rows[k].Power.IsVisible&&!rows[k].Ports.IsVisible&&!rows[k].Status.IsVisible).ToArray(),visibleCards=cards.Where(c=>c.Value.Visibility==Visibility.Visible).Select(c=>c.Key).ToArray(),ceilings=axisCeilings,curves=rows.ToDictionary(r=>r.Key,r=>r.Value.Lines.Count),plotted=rows.ToDictionary(r=>r.Key,r=>r.Value.Lines.Count(l=>l.Data is PathGeometry g&&g.Figures.Any(f=>f.Segments.Count>0))),charts=rows.ToDictionary(r=>r.Key,r=>r.Value.Lines.Select(l=>l.Parent).Distinct().Count()),colors=rows.ToDictionary(r=>r.Key,r=>r.Value.Lines.Select(l=>l.Stroke.ToString()).ToArray()),sharedAxes=rows.Values.All(r=>r.Lines.Select(l=>l.Parent).Distinct().Count()==1),totalColors=rows.ToDictionary(r=>r.Key,r=>r.Value.Power.Foreground.ToString()),hasCloseButton=HasButton(frame),hasContextMenu=frame.ContextMenu!=null};
 public void Shutdown(){SavePosition();internalClose=true;Close();}
 void PlaceDefault(){Left=SystemParameters.WorkArea.Right-Width-22;Top=SystemParameters.WorkArea.Top+30;}
 void RestorePosition(){var p=controller.Prefs;if(p.DesktopWidgetLeft is double x&&p.DesktopWidgetTop is double y&&double.IsFinite(x)&&double.IsFinite(y)){Left=x;Top=y;}else PlaceDefault();ClampPosition();}
 void ClampPosition(){if(Left+Width<SystemParameters.VirtualScreenLeft+40||Left>SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-40||Top+ActualHeight<SystemParameters.VirtualScreenTop+40||Top>SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-40)PlaceDefault();}
 void SavePosition(){if(!positionReady)return;controller.Prefs.DesktopWidgetLeft=Left;controller.Prefs.DesktopWidgetTop=Top;Store.Write("settings.json",controller.Prefs);}
 public void RefreshLatest(){if(paintedRevision!=controller.WidgetRevision)Refresh();}
 public void Refresh(DateTimeOffset? clock=null){paintedRevision=controller.WidgetRevision;
  RestoreOnDesktop();Topmost=controller.Prefs.DesktopWidgetTopmost;Cursor=controller.Prefs.DesktopWidgetLocked?Cursors.Arrow:Cursors.SizeAll;
  var states=controller.WidgetReadings();double sum=0;int online=0;bool demo=false;
  var now=clock??DateTimeOffset.UtcNow;
  foreach(var raw in states){
   var stamp=controller.LastWidgetLoad(raw);
   var mode=WidgetVisibility.Mode(raw.Connected,stamp,now);
   var active=stamp.HasValue&&stamp==raw.Updated?.ToUnixTimeMilliseconds()&&!raw.Stale;
   var live=mode!=WidgetCardMode.Hidden;
   var expanded=mode==WidgetCardMode.Expanded;
   cards[raw.Model].Visibility=live?Visibility.Visible:Visibility.Collapsed;var fresh=raw.Connected&&!raw.Stale;var key=raw.Model+":"+raw.Id+":"+raw.Demo+":"+mode+":"+active+":"+fresh+":"+controller.Prefs.PowerRefreshSeconds+":"+string.Join(",",raw.Ports.Select(p=>$"{p.On}/{p.Status}"));
   if(!live)shown.ClearKeysForModel(raw.Model,"");if(!active||!shown.TryGetValue(key,out var cached)||controller.Prefs.PowerRefreshSeconds==0||(now-cached.At).TotalSeconds>=controller.Prefs.PowerRefreshSeconds){cached=(now,raw);shown.ClearKeysForModel(raw.Model,key);shown[key]=cached;}
   var s=cached.State;var row=rows[raw.Model];row.Name.Text=raw.Alias??(raw.Model=="AD1204U"?"10 Ultra":raw.Model=="A2687"?"Prime 160W":"Prime 250W");if(fresh){if(expanded)sum+=raw.Total;online++;}demo|=s.Demo;
   row.CompactStatus.Text=raw.Stale?"等待新数据":"暂无有效输出";
   row.CompactStatus.Visibility=mode==WidgetCardMode.Compact?Visibility.Visible:Visibility.Collapsed;
   row.Power.Visibility=row.Status.Visibility=row.Ports.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;
   row.Power.Text=fresh?s.Total.ToString("F1")+" W":"— W";row.Status.Text=s.Demo?"模拟数据 · 不计入能耗":!raw.Connected?"未连接":raw.Stale?"等待新数据":raw.Model!="A2345"?"● 蓝牙已连接":"● 云端已连接";
   var h=controller.WidgetChart(raw).Select(p=>(At:DateTimeOffset.FromUnixTimeMilliseconds(p.At),Watts:p.PlotValues??p.Values,Accepted:p.PlotVisible??(p.PlotValues??p.Values).Select(v=>v>PowerCurve.Minimum).ToArray())).ToList();
   const double span=60;
   var visible=raw.Ports.Select((p,i)=>h.Any(sample=>sample.Watts[i]>PowerCurve.Minimum)).ToArray();
   var max=controller.ChartCeiling(raw);axisCeilings[raw.Model]=max;
   for(int i=0;i<s.Ports.Count;i++){var canvas=(Canvas)row.Lines[i].Parent;var width=Math.Max(1,canvas.ActualWidth);row.Lines[i].Data=PowerCurve.Geometry(h.Select(sample=>(X:Math.Clamp(1-(now-sample.At).TotalSeconds/span,0,1)*width,Watts:sample.Watts[i],Accepted:sample.Accepted[i])),max,visible[i]);row.Values[i].Text=fresh&&h.Count>0&&h[^1].Watts[i]>PowerCurve.Minimum&&s.Ports[i].On!=false&&s.Ports[i].Power>PowerCurve.Minimum?s.Ports[i].Power.ToString("F1"):"—";}
  }
  total.Text=online>0?sum.ToString("F1"):"—";caption.Text=demo?"模拟预览 · 非真实测量":online>0?$"合计输出 · {online} 台在线":"等待充电器连接";ClampPosition();
 }
}
internal static class WidgetCacheExtensions {
 public static void ClearKeysForModel<T>(this Dictionary<string,T> dictionary,string model,string except){foreach(var key in dictionary.Keys.Where(k=>k.StartsWith(model+":",StringComparison.Ordinal)&&k!=except).ToArray())dictionary.Remove(key);}
}
internal static class PowerCurve {
 public const double Minimum=.2;
 public static double Ceiling(IEnumerable<double> watts)=>Math.Ceiling(Math.Max(1,watts.Where(w=>w>Minimum).DefaultIfEmpty(0).Max())*1.15);
 public static PathGeometry Geometry(IEnumerable<(double X,double Watts)> samples,double ceiling,bool visible){
  return Geometry(samples.Select(s=>(s.X,s.Watts,Accepted:s.Watts>Minimum)),ceiling,visible);
 }
 public static PathGeometry Geometry(IEnumerable<(double X,double Watts,bool Accepted)> samples,double ceiling,bool visible){
  var geometry=new PathGeometry();if(!visible)return geometry;PathFigure? segment=null;
  foreach(var sample in samples){if(!sample.Accepted){segment=null;continue;}var point=new Point(sample.X,46-sample.Watts/ceiling*44);if(segment==null){segment=new PathFigure{StartPoint=point,IsFilled=false};geometry.Figures.Add(segment);}else segment.Segments.Add(new LineSegment(point,true));}
  return geometry;
 }
}
