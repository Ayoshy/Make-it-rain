namespace Battlestation;
internal sealed class CountdownSurface : Surface
{
    public CountdownSurface(Station station):base(station){Width=779;Height=706;}
    protected override void Paint()
    {
        Text("—  VICE CITY IS CALLING  —",389,0,11,"#E8CBD7",align:"center",bold:true,tracking:4.44);
        Image(System.IO.Path.Combine(Station.Assets,"Images/logo.png"),86,40,607,406);
        var keys=new[]{"days","hours","minutes","seconds"};var labels=new[]{"JOURS","HEURES","MINUTES","SECONDES"};
        for(int i=0;i<4;i++)
        {
            Text(Station.M(keys[i]),90+i*196,470,86,i==0?"#EF9FDA":i==3?"#E4C8E0":"#FFF0D9","GTAArtDeco","center",true,tracking:2.88);
            Text(labels[i],90+i*196,615,10.5,"#CDB1C7",align:"center",tracking:3.1);
            if(i<3)Box(184+i*196,528,1,43,"#37FFDEE6");
        }
        Text("—  "+Station.M("date")+"  —",389,659,13.5,"#EFCBE5",align:"center",bold:true,tracking:3.94);
    }
}
