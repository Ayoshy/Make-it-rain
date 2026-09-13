(function(root){
  'use strict';
  function fract(v){return v-Math.floor(v);}
  function rgba(c,a){return 'rgba('+c.join(',')+','+a+')';}
  function Overlays(canvas){
    this.canvas=canvas;
    this.ctx=canvas.getContext('2d');
    this.width=1;this.height=1;
    this.colors=[[255,180,112],[255,110,191],[117,218,255]];
    this.sprites=[];this.bokehSprites=[];
    this.dust=Array.from({length:180},function(_,i){return {
      x:fract(i*.61803398875+.09),y:fract(i*.754877666+.31),
      depth:fract(i*.414213562+.12),phase:i*2.39996,speed:.007+(i%7)*.0014
    };});
    if(!this.ctx)return;
    var self=this;
    this.colors.forEach(function(color){
      self.sprites.push(self.makeSprite(color,false));
      self.bokehSprites.push(self.makeSprite(color,true));
    });
  }
  Overlays.enabled=function(s){
    return s.fxintensity>0&&((s.particles&&s.particledensity>0)||s.bokeh||s.lightbeams||s.lightstreaks);
  };
  Overlays.prototype.makeSprite=function(color,bokeh){
    var sprite=document.createElement('canvas');sprite.width=sprite.height=96;
    var c=sprite.getContext('2d');
    var g=c.createRadialGradient(48,48,0,48,48,48);
    if(bokeh){
      g.addColorStop(0,rgba(color,.2));g.addColorStop(.48,rgba(color,.28));
      g.addColorStop(.72,rgba(color,.25));g.addColorStop(1,rgba(color,0));
    }else{
      g.addColorStop(0,'rgba(255,247,234,1)');g.addColorStop(.12,rgba(color,.9));
      g.addColorStop(.32,rgba(color,.4));g.addColorStop(.65,rgba(color,.09));g.addColorStop(1,rgba(color,0));
    }
    c.fillStyle=g;c.fillRect(0,0,96,96);return sprite;
  };
  Overlays.prototype.resize=function(w,h){
    this.width=w;this.height=h;
    // Soft light does not need a 4K backing canvas. Keep compositing bounded.
    var scale=Math.min(1,1920/w,1080/h);
    this.canvas.width=Math.max(1,Math.round(w*scale));
    this.canvas.height=Math.max(1,Math.round(h*scale));
    if(this.ctx)this.ctx.setTransform(scale,0,0,scale,0,0);
  };
  Overlays.prototype.quietZone=function(x,y,rect){
    if(!rect)return 1;
    var dx=(x-(rect.left+rect.width/2))/(rect.width*.68);
    var dy=(y-(rect.top+rect.height/2))/(rect.height*.7);
    return .25+.75*(1-Math.exp(-(dx*dx+dy*dy)*1.7));
  };
  Overlays.prototype.render=function(time,s,rect){
    var c=this.ctx;if(!c)return;
    var w=this.width,h=this.height,unit=Math.min(w/1920,h/1080);
    c.clearRect(0,0,w,h);
    if(!Overlays.enabled(s))return;
    c.save();c.globalCompositeOperation='screen';
    if(s.lightbeams){
      for(var b=0;b<3;b++){
        c.save();
        c.translate(w*(.26+b*.28+Math.sin(time*.075+b)*.05),-h*.3);
        c.rotate(.3+Math.sin(time*.06+b*.9)*.14);
        var bw=w*(.032+b*.01);
        var color=this.colors[s.theme==='night'?(b+1)%3:b%2];
        var beam=c.createLinearGradient(-bw,0,bw,0);
        beam.addColorStop(0,rgba(color,0));beam.addColorStop(.48,rgba(color,.19));
        beam.addColorStop(.52,rgba(color,.19));beam.addColorStop(1,rgba(color,0));
        c.globalAlpha=.6+.3*Math.sin(time*.16+b);c.fillStyle=beam;
        c.fillRect(-bw,0,bw*2,h*1.8);c.restore();
      }
    }
    if(s.bokeh){
      for(var i=0;i<14;i++){
        var d=this.dust[i*7],x=(d.x+Math.sin(time*.07+d.phase)*.045)*w;
        var y=(fract(d.y-time*.0025)*1.25-.125)*h;
        var r=(25+d.depth*65)*unit;
        c.globalAlpha=(.33+.25*Math.sin(time*.14+d.phase))*this.quietZone(x,y,rect);
        c.drawImage(this.bokehSprites[i%3],x-r,y-r,r*2,r*2);
      }
    }
    if(s.particles){
      var count=Math.round(s.particledensity*1.8);
      for(var j=0;j<count;j++){
        var dot=this.dust[j];
        var dx=(fract(dot.x+time*.0012*(dot.depth+.2)+Math.sin(time*.11+dot.phase)*.013)*1.08-.04)*w;
        var dy=(fract(dot.y-time*dot.speed)*1.12-.06)*h;
        var radius=(2.8+dot.depth*8)*unit;
        var twinkle=.5+.5*Math.pow((Math.sin(time*.7+dot.phase)+1)/2,2);
        c.globalAlpha=(.35+dot.depth*.6)*twinkle*this.quietZone(dx,dy,rect);
        c.drawImage(this.sprites[j%3],dx-radius,dy-radius,radius*2,radius*2);
      }
    }
    if(s.lightstreaks){
      for(var k=0;k<3;k++){
        var cycle=22+k*5;
        var phase=fract((time+k*9+3)/cycle);
        if(phase>.34)continue;
        var progress=phase/.34;
        var sx=(-.15+progress*1.4)*w,sy=(.86-k*.25-progress*.18)*h;
        var length=(145+k*40)*unit;
        c.save();c.translate(sx,sy);c.rotate(-.16);
        var line=c.createLinearGradient(-length,0,0,0);
        line.addColorStop(0,rgba(this.colors[k],0));
        line.addColorStop(.7,rgba(this.colors[k],.52));
        line.addColorStop(1,'rgba(255,238,224,.95)');
        c.globalAlpha=Math.sin(progress*Math.PI)*this.quietZone(sx,sy,rect)*.65;
        c.strokeStyle=line;c.lineCap='round';c.lineWidth=unit*1.3;
        c.beginPath();c.moveTo(-length,0);c.lineTo(0,0);c.stroke();
        c.globalAlpha*=.2;c.lineWidth=unit*8;c.stroke();
        c.globalAlpha*=4;c.drawImage(this.sprites[k],-9*unit,-9*unit,18*unit,18*unit);
        c.restore();
      }
    }
    c.restore();
  };
  root.ViceOverlays=Overlays;
})(typeof window!=='undefined'?window:globalThis);
