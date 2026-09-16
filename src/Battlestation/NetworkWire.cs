namespace Battlestation;
internal sealed record NetworkAppRate(string Name,double Received,double Sent,int Processes);
internal sealed record NetworkDetailFrame(bool Active,string Status,NetworkAppRate[] Apps,long LostEvents=0);
