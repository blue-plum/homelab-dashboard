using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ArpResult {public string Mac="",Address="",Message="";}
public static class ArpDiscovery {
 [DllImport("iphlpapi.dll",ExactSpelling=true)]static extern uint SendARP(uint destination,uint source,[Out] byte[] mac,ref uint length);
 public static string Normalize(string value){string v=(value??"").Trim().Replace(":","").Replace("-","").Replace(".","").Replace(" ","");return v.Length==12&&v.All(Uri.IsHexDigit)?v.ToUpperInvariant():"";}
 public static string Compare(string saved,string observed){if(Normalize(observed).Length==0)return "No MAC resolved";if(String.IsNullOrWhiteSpace(saved))return "No saved MAC";if(Normalize(saved).Length==0)return "Saved MAC format not recognised";return Normalize(saved)==Normalize(observed)?"MAC matches":"MAC MISMATCH";}
 public static bool SameSubnet(IPAddress a,IPAddress b,IPAddress mask){if(a.AddressFamily!=AddressFamily.InterNetwork||b.AddressFamily!=AddressFamily.InterNetwork||mask==null||mask.AddressFamily!=AddressFamily.InterNetwork)return false;var aa=a.GetAddressBytes();var bb=b.GetAddressBytes();var mm=mask.GetAddressBytes();return mm.Any(x=>x!=0)&&Enumerable.Range(0,4).All(i=>(aa[i]&mm[i])==(bb[i]&mm[i]));}
 public static Task<ArpResult> ResolveAsync(string address){return Task.Run(()=>Resolve(address));}
 static ArpResult Resolve(string address){var result=new ArpResult();if(String.IsNullOrWhiteSpace(address)){result.Message="ARP skipped: no address";return result;}try{IPAddress direct;var targets=IPAddress.TryParse(address,out direct)?new[]{direct}:Dns.GetHostAddresses(address);var ipv4=targets.Where(ip=>ip.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(ip)&&ip.GetAddressBytes()[0]>0&&ip.GetAddressBytes()[0]<224).ToArray();if(ipv4.Length==0){result.Message="ARP unavailable: requires a local IPv4 address";return result;}
  var candidates=new List<Tuple<IPAddress,IPAddress,int>>();foreach(var nic in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)){foreach(var local in nic.GetIPProperties().UnicastAddresses.Where(x=>x.Address.AddressFamily==AddressFamily.InterNetwork)){foreach(var target in ipv4)if(SameSubnet(target,local.Address,local.IPv4Mask)){var mask=local.IPv4Mask.GetAddressBytes();int bits=mask.Sum(x=>Convert.ToString(x,2).Count(c=>c=='1'));candidates.Add(Tuple.Create(target,local.Address,bits));}}}
  if(candidates.Count==0){result.Message="ARP skipped: outside directly connected IPv4 subnets";return result;}foreach(var candidate in candidates.OrderByDescending(x=>x.Item3)){byte[] mac=new byte[8];uint length=(uint)mac.Length;uint code=SendARP(BitConverter.ToUInt32(candidate.Item1.GetAddressBytes(),0),BitConverter.ToUInt32(candidate.Item2.GetAddressBytes(),0),mac,ref length);result.Address=candidate.Item1.ToString();if(code==0&&length==6&&mac.Take(6).Any(x=>x!=0)&&(mac[0]&1)==0){result.Mac=String.Join(":",mac.Take(6).Select(x=>x.ToString("X2")));result.Message="ARP resolved (Windows may use its cache)";return result;}result.Message="ARP unresolved (Windows error "+code+")";}return result;
 }catch(Exception ex){result.Message="ARP unavailable: "+ex.Message;return result;}}
 public static string Describe(NetworkConnection c,ConnectionCheck check){if(check==null||check.Arp==null)return "ARP not checked";var a=check.Arp;return a.Mac.Length==0?a.Message:"ARP MAC: "+a.Mac+" — "+Compare(DeviceConnections.Get(c.Details,"MAC"),a.Mac)+"\r\n"+a.Address+" · "+a.Message;}
}
