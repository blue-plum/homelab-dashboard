using System;
using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Threading.Tasks;

public class PortResult {public string Service,Host,Result;public int Port;public bool Open;public override string ToString(){return Service+" "+Host+":"+Port+" — "+Result;}}
public static class PortChecks {
 public static List<PortResult> Targets(NetworkConnection c){var a=c.Access??AccessOptions.Defaults();var list=new List<PortResult>();Uri url;if(!String.IsNullOrWhiteSpace(c.Url)&&Uri.TryCreate(c.Url,UriKind.Absolute,out url))list.Add(new PortResult{Service="Dashboard",Host=url.Host.Trim('[',']'),Port=url.Port});foreach(var kind in new[]{"SSH","RDP","Shared"}){string host=Links.Host(c,kind);if(!String.IsNullOrWhiteSpace(host))list.Add(new PortResult{Service=kind=="Shared"?"SMB":kind,Host=host,Port=kind=="SSH"?a.SshPort:kind=="RDP"?a.RdpPort:445});}return list;}
 static void Observe(Task task){task.ContinueWith(t=>{var error=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
 public static async Task<PortResult> Probe(PortResult target){using(var client=new TcpClient()){try{var task=client.ConnectAsync(target.Host,target.Port);if(await Task.WhenAny(task,Task.Delay(1000))!=task){target.Result="Timed out";client.Close();Observe(task);return target;}await task;target.Open=true;target.Result="Open";}catch(SocketException ex){target.Result=ex.SocketErrorCode==SocketError.ConnectionRefused?"Refused":ex.SocketErrorCode==SocketError.TimedOut?"Timed out":"Unavailable ("+ex.SocketErrorCode+")";}catch(Exception){target.Result="Unavailable";}return target;}}
 public static async Task<List<PortResult>> Check(NetworkConnection c){return (await Task.WhenAll(Targets(c).Select(Probe))).ToList();}
 public static string Describe(ConnectionCheck check){return check==null||check.Ports==null?"TCP ports not checked":check.Ports.Count==0?"No TCP targets configured":String.Join("\r\n",check.Ports.Select(p=>p.ToString()));}
}
