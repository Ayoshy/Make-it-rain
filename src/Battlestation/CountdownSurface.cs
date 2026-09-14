namespace Battlestation;
internal sealed class CountdownSurface : Surface
{
    public CountdownSurface(Station station):base(station){Width=779;Height=706;}
    protected override void Paint()
    {
        double w=Width,h=Height;bool compact=w<620;
        Text("—  VICE CITY IS CALLING  —",w/2,4,compact?9:11,"#E8CBD7",align:"center",bold:true,tracking:compact?1:4.44);
        double logoScale=Math.Min((w-64)/607,(h-210)/406),logoW=607*logoScale,logoH=406*logoScale;
        Image(System.IO.Path.Combine(Station.Assets,"Images/logo.png"),(w-logoW)/2,32,logoW,logoH);
        var keys=new[]{"days","hours","minutes","seconds"};var labels=new[]{"JOURS","HEURES","MINUTES","SECONDES"};
        for(int i=0;i<4;i++)
        {
            Text(Station.M(keys[i]),(i+.5)*w/4,h-190,compact?46:74,i==0?"#EF9FDA":i==3?"#E4C8E0":"#FFF0D9","GTAArtDeco","center",true,tracking:1);
            Text(labels[i],(i+.5)*w/4,h-66,compact?8:10.5,"#CDB1C7",align:"center",tracking:compact?.5:2);
            if(i<3)Box((i+1)*w/4,h-155,1,32,"#37FFDEE6");
        }
        Text("—  "+Station.M("date")+"  —",w/2,h-30,compact?10:13.5,"#EFCBE5",align:"center",bold:true,tracking:compact?1:3);
    }
}
