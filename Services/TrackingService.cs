using System.Net.NetworkInformation;
using System.Net.Sockets;
using GovernmentMiningApp.Models;

namespace GovernmentMiningApp.Services;

/// <summary>Authorized network observation. Identity/address/phone require an authorized official source.</summary>
public sealed class TrackingService
{
    private readonly DatabaseService _db;
    public static readonly int[] MinerPorts={3333,4028,4444,5555,7777,8008,8080,8333,8443,8545,8888,9332,9333,9999,14433,14444,18080,19332,19333};
    public static readonly MinerProfile[] KnownMiners={new(){Key="S19Pro",Name="Antminer S19 Pro",HashRate="110TH/s",PowerWatts=1450},new(){Key="S19",Name="Antminer S19",HashRate="95TH/s",PowerWatts=1326},new(){Key="L7",Name="Antminer L7",HashRate="9.5GH/s",PowerWatts=3425},new(){Key="M50",Name="Whatsminer M50",HashRate="112TH/s",PowerWatts=3418},new(){Key="A1366",Name="Canaan A1366",HashRate="70TH/s",PowerWatts=3250},new(){Key="KAS",Name="IceRiver KAS",HashRate="4.7PH/s",PowerWatts=3600},new(){Key="E9",Name="Antminer E9",HashRate="2.4GH/s",PowerWatts=2400},new(){Key="GPU",Name="GPU Mining Rig",HashRate="Variable",PowerWatts=1500}};
    public event EventHandler<ScanProgressEventArgs>? ProgressChanged;
    public TrackingService(DatabaseService db)=>_db=db;
    public Task<List<DetectedDevice>> ScanAsync(string operationCode,IReadOnlyList<string> targetIps,CancellationToken ct=default)=>RunAuthorizedScanAsync(operationCode,"",targetIps,MinerPorts,false,AppSession.CurrentUser?.FullName??"سیستم",ct);

    public async Task<List<DetectedDevice>> RunAuthorizedScanAsync(string operationCode,string region,IReadOnlyList<string> targetIps,IReadOnlyList<int> ports,bool simulationMode,string authorizedBy,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(operationCode))throw new ArgumentException("کد عملیاتی الزامی است.");
        if(!AppSession.IsAuthenticated)throw new UnauthorizedAccessException("اسکن فقط برای کاربر احراز‌شده مجاز است.");
        if(simulationMode&&!string.Equals(Environment.GetEnvironmentVariable("CKASHEF_ALLOW_SIMULATION"),"1",StringComparison.Ordinal))throw new InvalidOperationException("حالت شبیه‌سازی در محیط عملیاتی غیرفعال است.");
        if(targetIps.Count==0&&!simulationMode)throw new ArgumentException("لیست IP هدف خالی است.");
        var start=DateTime.Now;var found=new List<DetectedDevice>();var portsToUse=ports.Count>0?ports.ToArray():MinerPorts;var rnd=new Random();
        if(simulationMode){var n=rnd.Next(3,12);for(var i=0;i<n;i++){ct.ThrowIfCancellationRequested();await Task.Delay(180,ct);var m=KnownMiners[rnd.Next(KnownMiners.Length)];var d=BuildSimulation(operationCode,region,$"SIM-{i+1:000}",portsToUse[rnd.Next(portsToUse.Length)],m,rnd);found.Add(d);_db.SaveDetection(d);Report(i+1,n,found.Count,$"SIMULATION: {d.IPAddress}");}}
        else{var total=targetIps.Count*portsToUse.Length;var scanned=0;foreach(var raw in targetIps){ct.ThrowIfCancellationRequested();var ip=raw.Trim();if(!IsValidIp(ip)){scanned+=portsToUse.Length;continue;}foreach(var port in portsToUse){ct.ThrowIfCancellationRequested();scanned++;if(await IsPortOpenAsync(ip,port,600,ct)){var d=BuildObservation(operationCode,region,ip,port);found.Add(d);_db.SaveDetection(d);}if(scanned%3==0||scanned==total)Report(scanned,total,found.Count,$"بررسی {ip}:{port}");}}}
        _db.SaveScanHistory(operationCode,simulationMode?"Simulation":"AuthorizedScan",start,DateTime.Now,simulationMode?found.Count:targetIps.Count*portsToUse.Length,found.Count,$"مجازکننده: {authorizedBy} | منطقه: {region}");Report(100,100,found.Count,simulationMode?"شبیه‌سازی تکمیل شد":"اسکن تکمیل شد");return found;
    }
    public async Task<List<DetectedDevice>> QuickLocalProbeAsync(string operationCode,string region,CancellationToken ct=default){var ips=GetLocalIpv4Addresses();return await RunAuthorizedScanAsync(operationCode,region,ips,new[]{3333,4028,8333,9332,8080},false,AppSession.CurrentUser?.FullName??"سیستم",ct);}
    private static DetectedDevice BuildObservation(string op,string region,string ip,int port)=>new(){OperationID=Guid.NewGuid().ToString("N"),OperationCode=op,IPAddress=ip,Port=port,Province=region??"",DeviceModel="تأیید نشده",HashRate="تأیید نشده",EstimatedConsumption=0,DetectionTime=DateTime.Now,ActionStatus="منتظر‌دستورالعمل",Confidence=1.0,EvidenceType="NetworkObservation",DataSource="NetworkProbe",MatchStatus="Unresolved",ActionNotes="وجود سرویس شبکه مشاهده شد؛ مدل دستگاه، توان مصرفی و هویت مشترک از این مشاهده قابل استنتاج نیست."};
    private static DetectedDevice BuildSimulation(string op,string region,string ip,int port,MinerProfile m,Random rnd)=>new(){OperationID=Guid.NewGuid().ToString("N"),OperationCode=op,IPAddress=ip,Port=port,Province=region??"",DeviceModel=m.Name,HashRate=m.HashRate,EstimatedConsumption=m.PowerWatts,DetectionTime=DateTime.Now,ActionStatus="منتظر‌دستورالعمل",Confidence=Math.Round(.72+rnd.NextDouble()*.25,2),EvidenceType="Simulation",DataSource="Synthetic",MatchStatus="Simulation",ActionNotes="SIMULATION — داده مصنوعی است و برای واقعیت عملیاتی معتبر نیست."};
    private static bool IsValidIp(string ip)=>System.Net.IPAddress.TryParse(ip,out var a)&&a.AddressFamily==AddressFamily.InterNetwork;
    private static async Task<bool> IsPortOpenAsync(string ip,int port,int timeoutMs,CancellationToken ct){try{using var c=new TcpClient();using var x=CancellationTokenSource.CreateLinkedTokenSource(ct);x.CancelAfter(timeoutMs);await c.ConnectAsync(ip,port,x.Token);return c.Connected;}catch{return false;}}
    private static List<string> GetLocalIpv4Addresses(){var list=new List<string>();try{foreach(var ni in NetworkInterface.GetAllNetworkInterfaces()){if(ni.OperationalStatus!=OperationalStatus.Up)continue;foreach(var ua in ni.GetIPProperties().UnicastAddresses)if(ua.Address.AddressFamily==AddressFamily.InterNetwork&&!list.Contains(ua.Address.ToString()))list.Add(ua.Address.ToString());}}catch{}return list;}
    private void Report(int current,int total,int found,string message)=>ProgressChanged?.Invoke(this,new ScanProgressEventArgs{Percent=total<=0?0:(int)Math.Min(100,current*100.0/total),Message=message,Found=found,Scanned=current});
}
