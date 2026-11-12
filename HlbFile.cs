using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Web.Script.Serialization;

public class HlbDocument {
 public string Format="Homelab Network";public int Version=2;
 public List<Device> Devices=new List<Device>();public NetworkMap Layout=new NetworkMap();
 public Dictionary<string,List<string>> Lookups=LookupStore.Defaults();[ScriptIgnore] public UiPreferences Preferences=new UiPreferences();
 public Dictionary<string,Dictionary<string,string>> Mnemonics=new Dictionary<string,Dictionary<string,string>>();
 public Dictionary<string,int> ReferenceCounters=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
 public int LayoutZoom=100;public float PanX=25,PanY=25;
}
public static class HlbFile {
 static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=134217728,RecursionLimit=100};}
 public static string Encode(HlbDocument document){return Serializer().Serialize(document);}
 public static HlbDocument Decode(string json){var raw=Serializer().Deserialize<Dictionary<string,object>>(json);if(raw==null||!raw.ContainsKey("Format")||(string)raw["Format"]!="Homelab Network"||!raw.ContainsKey("Version")||(Convert.ToInt32(raw["Version"])!=1&&Convert.ToInt32(raw["Version"])!=2)||!raw.ContainsKey("Devices")||!raw.ContainsKey("Layout")||!raw.ContainsKey("Lookups"))throw new Exception("This is not a supported Homelab (.hlb) file.");var doc=Serializer().Deserialize<HlbDocument>(json);if(doc.Devices==null||doc.Layout==null||doc.Lookups==null||doc.Preferences==null)throw new Exception("Network file is incomplete.");doc.Preferences=UiPreferences.Current;doc.Version=2;if(doc.Mnemonics==null||doc.ReferenceCounters==null)throw new Exception("Invalid reference settings.");foreach(var item in doc.Mnemonics)if(item.Value==null||item.Value.Any(x=>String.IsNullOrWhiteSpace(x.Key)||x.Value==null||x.Value.Length>30||x.Value.IndexOfAny(new[]{'/','\\'})>=0||x.Value.Any(Char.IsControl)))throw new Exception("Invalid mnemonic.");if(doc.ReferenceCounters.Any(x=>String.IsNullOrWhiteSpace(x.Key)||x.Value<0||x.Value>=Int32.MaxValue))throw new Exception("Invalid reference counter.");var normalized=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);foreach(var c in doc.ReferenceCounters){int old;normalized.TryGetValue(c.Key,out old);normalized[c.Key]=Math.Max(old,c.Value);}doc.ReferenceCounters=normalized;var p=doc.Preferences;if(!new[]{"Tiles","List","Layout"}.Contains(p.View)||p.Zoom<20||p.Zoom>200||p.SshPort<1||p.SshPort>65535||String.IsNullOrWhiteSpace(p.SshClient)||String.IsNullOrWhiteSpace(p.RdpClient))throw new Exception("Invalid network preferences.");
  var previous=UiPreferences.Current;try{UiPreferences.Current=p;doc.Devices=Config.Decode(Config.Encode(doc.Devices));}finally{UiPreferences.Current=previous;}
  doc.Layout=NetworkMap.Decode(doc.Layout.Encode());if(doc.Lookups.Any(x=>String.IsNullOrWhiteSpace(x.Key)||x.Value==null||x.Value.Count>LookupStore.MaxEntries||x.Value.Any(v=>String.IsNullOrWhiteSpace(v)||v.Length>200)))throw new Exception("Invalid lookup lists.");foreach(var pair in LookupStore.Defaults())if(!doc.Lookups.ContainsKey(pair.Key))doc.Lookups[pair.Key]=pair.Value;
  if(doc.LayoutZoom<20||doc.LayoutZoom>200||float.IsNaN(doc.PanX)||float.IsInfinity(doc.PanX)||float.IsNaN(doc.PanY)||float.IsInfinity(doc.PanY))throw new Exception("Invalid layout view.");return doc;
 }
 public static HlbDocument Read(string path){if(new FileInfo(path).Length>134217728)throw new Exception("Network file exceeds the 128 MB limit.");return Decode(File.ReadAllText(path));}
 public static void Write(string path,HlbDocument doc){string json=Encode(doc);Decode(json);if(System.Text.Encoding.UTF8.GetByteCount(json)>134217728)throw new Exception("Network file exceeds the 128 MB limit.");string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,json);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static HlbDocument ReadLegacy(){var file=new[]{Config.PathName,Config.PreviousPath,Config.OlderPath,Config.LegacyPath}.FirstOrDefault(File.Exists);if(file==null)throw new Exception("No previous network data was found on this computer.");var doc=new HlbDocument{Preferences=UiPreferences.Load()};var prior=UiPreferences.Current;try{UiPreferences.Current=doc.Preferences;doc.Devices=Config.Decode(File.ReadAllText(file));}finally{UiPreferences.Current=prior;}string layout=Path.Combine(Config.Folder,"layout-v1.json");if(File.Exists(layout))doc.Layout=NetworkMap.Decode(File.ReadAllText(layout));doc.Lookups=new LookupStore(Path.Combine(Config.Folder,"lookups.json")).Lists;return Decode(Encode(doc));}
}
public partial class Dashboard {
 HlbDocument document=new HlbDocument();string networkFile;bool dirty;int documentGeneration;bool applyingDocument;
 void MarkDirty(){if(applyingDocument)return;dirty=true;UpdateDocumentTitle();}
 void UpdateDocumentTitle(){Text="Homelab Dashboard v8.6 — "+(networkFile==null?(dirty?"Untitled network":"No network loaded"):Path.GetFileName(networkFile))+(dirty?" *":"");}
 HlbDocument CaptureDocument(){document.Devices=devices;document.Lookups=LookupStore.DocumentLists??LookupStore.Defaults();if(topology!=null){document.Layout=topology.Canvas.Map;document.LayoutZoom=(int)Math.Round(topology.Canvas.ZoomScale*100);var pan=topology.Canvas.Pan;document.PanX=pan.X;document.PanY=pan.Y;}return document;}
 void ApplyDocument(HlbDocument next,string file,bool modified){modified=DeviceReferences.Assign(next)||modified;applyingDocument=true;try{selectedDeviceId=null;documentGeneration++;CheckProgress(checking?"Status check cancelled — network changed.":"Ready");if(topology!=null){topology.Dispose();topology=null;}document=next;DeviceReferences.Current=document;devices=next.Devices;networkFile=file;LookupStore.DocumentLists=next.Lookups;LookupStore.DocumentChanged=MarkDirty;connectionChecks.Clear();statuses.Clear();statusDetails.Clear();search.Clear();UpdateGroups();viewMode.SelectedItem=UiPreferences.Current.View;listMode=viewMode.SelectedIndex==1;ShowLayout(viewMode.SelectedIndex==2);SetZoom(UiPreferences.Current.Zoom);if(topology!=null){topology.Canvas.SetZoom(UiPreferences.Current.Zoom);topology.Canvas.Pan=new PointF(next.PanX,next.PanY);zoom=UiPreferences.Current.Zoom;zoomLabel.Text=zoom+"%";topology.Canvas.Invalidate();}Render();dirty=modified;UpdateDocumentTitle();}finally{applyingDocument=false;}}
 protected virtual DialogResult AskSaveChanges(){return MessageBox.Show(this,"Save changes to "+(networkFile==null?"this untitled network":Path.GetFileName(networkFile))+" before continuing?","Unsaved network changes",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);}
 bool ConfirmNetworkChange(){if(!dirty)return true;var choice=AskSaveChanges();return choice==DialogResult.No||(choice==DialogResult.Yes&&SaveNetwork(false));}
 protected virtual string ChooseNetworkLoadPath(){using(var picker=new OpenFileDialog{Filter="Homelab network (*.hlb)|*.hlb",Title="Load network"})return picker.ShowDialog(this)==DialogResult.OK?picker.FileName:null;}
 protected virtual string ChooseNetworkSavePath(string target){using(var picker=new SaveFileDialog{Filter="Homelab network (*.hlb)|*.hlb",DefaultExt="hlb",AddExtension=true,FileName=target==null?"Home Network.hlb":Path.GetFileName(target),Title="Save network"})return picker.ShowDialog(this)==DialogResult.OK?picker.FileName:null;}
 void LoadNetwork(){string path=ChooseNetworkLoadPath();if(path==null)return;try{HlbFile.Read(path);if(!ConfirmNetworkChange())return;ApplyDocument(HlbFile.Read(path),path,false);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not load network");}}
 bool SaveNetwork(bool saveAs){string target=networkFile;if(saveAs||target==null){target=ChooseNetworkSavePath(target);if(target==null)return false;}try{if(!String.Equals(Path.GetExtension(target),".hlb",StringComparison.OrdinalIgnoreCase))throw new Exception("Use the .hlb extension for network files.");HlbFile.Write(target,CaptureDocument());networkFile=target;dirty=false;UpdateDocumentTitle();if(topology!=null)topology.Canvas.ReportSaved();return true;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not save network");return false;}}
 void CloseNetwork(){if(ConfirmNetworkChange())ApplyDocument(new HlbDocument(),null,false);}
 void ImportPreviousNetwork(){try{var next=HlbFile.ReadLegacy();if(!ConfirmNetworkChange())return;ApplyDocument(next,null,true);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not import previous network");}}
 protected override void OnFormClosing(FormClosingEventArgs e){if(!e.Cancel&&!ConfirmNetworkChange())e.Cancel=true;base.OnFormClosing(e);}
}
