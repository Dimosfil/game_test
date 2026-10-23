using System;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    /// <summary>Deterministic map decoration. Only RuleSet.Blocks are raised obstacles.</summary>
    public static class WorldArt
    {
        public static GameObject Create(RuleSet rules, Transform parent)
        {
            if(rules==null) throw new ArgumentNullException(nameof(rules));
            if(rules.MapWidth<=0 || rules.MapHeight<=0) throw new ArgumentException("Map dimensions must be positive.",nameof(rules));
            var root=new GameObject("WorldArt");root.transform.SetParent(parent,false);
            var p=new ArtPalette(-1);
            var ground=ArtPalette.Get("desert-ground",new Color(.245f,.235f,.205f));
            var stone=ArtPalette.Get("plateau-stone",new Color(.34f,.3f,.245f));
            var cliff=ArtPalette.Get("cliff-shadow",new Color(.24f,.225f,.21f));
            var ridge=ArtPalette.Get("cliff-ridge",new Color(.42f,.375f,.3f));
            var road=ArtPalette.Get("road",new Color(.19f,.215f,.22f),.1f);
            var dust=ArtPalette.Get("dust",new Color(.32f,.295f,.24f));
            var seam=ArtPalette.Get("grid-seam",new Color(.27f,.26f,.225f));
            var boundary=ArtPalette.Get("boundary-light",new Color(.45f,.32f,.11f),.1f,new Color(.36f,.21f,.045f));
            var b=new ArtBuilder(root.transform,"Terrain");
            float width=rules.MapWidth,height=rules.MapHeight;
            b.Box(cliff,V(width*.5f,-.7f,height*.5f),V(width+5,1.25f,height+5));
            b.Box(ground,V(width*.5f,-.09f,height*.5f),V(width,.18f,height));

            // Broad low-contrast geological patches, rather than a noisy tactical surface.
            var random=new System.Random(77201);
            for(int z=0;z<rules.MapHeight;z+=6) for(int x=0;x<rules.MapWidth;x+=6)
            {
                float sx=Mathf.Min(6,width-x),sz=Mathf.Min(6,height-z);
                int variant=random.Next(5);
                var patch=ArtPalette.Get("ground-patch-"+variant,
                    Color.Lerp(new Color(.235f,.225f,.195f),new Color(.265f,.253f,.22f),variant*.25f));
                b.Box(patch,V(x+sx*.5f,.003f,z+sz*.5f),V(sx,.006f,sz));
            }
            for(int x=8;x<rules.MapWidth;x+=8) b.Box(seam,V(x,.009f,height*.5f),V(.018f,.005f,height));
            for(int z=8;z<rules.MapHeight;z+=8) b.Box(seam,V(width*.5f,.009f,z),V(width,.005f,.018f));

            // A worn logistical spine gives orientation; cut it wherever authoritative rock blocks lie.
            int roadZ=rules.MapHeight/2;
            for(int x=1;x<rules.MapWidth-1;x++)
            {
                if(InBlock(rules,x+.5f,roadZ,.9f)) continue;
                b.Box(road,V(x+.5f,.017f,roadZ),V(.99f,.013f,2.15f));
                for(int s=-1;s<=1;s+=2) b.Box(dust,V(x+.5f,.026f,roadZ+s*.98f),V(.6f,.005f,.045f));
                if(x%4==0) b.Box(p.White,V(x+.5f,.028f,roadZ),V(.55f,.005f,.065f));
            }

            // Landing aprons are surface markings, never additional gameplay obstructions.
            if(rules.Spawns!=null) foreach(var spawn in rules.Spawns)
            {
                if(spawn.DefinitionId!="command")continue;
                float x=spawn.Position.X*.001f,z=spawn.Position.Z*.001f;
                var team=new ArtPalette(spawn.Owner);
                b.Octagon(road,V(x,.018f,z),V(4.0f,.015f,4.0f));
                for(int i=0;i<8;i++)
                {
                    var q=Quaternion.Euler(0,i*45,0);
                    b.Box(team.Glow,V(x,0,z)+q*V(0,.039f,3.8f),V(.63f,.015f,.055f),V(0,i*45,0));
                    b.Box(dust,V(x,0,z)+q*V(0,.033f,3.33f),V(.055f,.01f,.63f),V(0,i*45,0));
                }
            }

            if(rules.Blocks!=null) foreach(var block in rules.Blocks)
            {
                float sx=block.Width,sz=block.Height;
                float cx=block.X+sx*.5f,cz=block.Z+sz*.5f;
                b.Box(cliff,V(cx,.24f,cz),V(sx,.48f,sz));
                // Every visible overhang remains inside the simulation's rectangle.
                b.Box(stone,V(cx,.67f,cz),V(Mathf.Max(.1f,sx-.16f),.68f,Mathf.Max(.1f,sz-.16f)));
                b.Box(ridge,V(cx,1.01f,cz),V(Mathf.Max(.1f,sx-.32f),.06f,Mathf.Max(.1f,sz-.32f)));
                int count=Mathf.Max(1,Mathf.FloorToInt(sx*sz/5f));
                for(int i=0;i<count;i++)
                {
                    float rx=.3f+(float)random.NextDouble()*.55f;
                    float rz=.3f+(float)random.NextDouble()*.55f;
                    rx=Mathf.Min(rx,sx*.4f);rz=Mathf.Min(rz,sz*.4f);
                    float px=cx+((float)random.NextDouble()*2-1)*Mathf.Max(0,sx*.5f-rx);
                    float pz=cz+((float)random.NextDouble()*2-1)*Mathf.Max(0,sz*.5f-rz);
                    float rh=.35f+(float)random.NextDouble()*.8f;
                    b.Octagon(i%3==0?ridge:stone,V(px,1.02f+rh*.5f,pz),V(rx,rh,rz));
                }
                // Strata make the exposed walls recognizable as terrain from a high camera.
                for(int side=-1;side<=1;side+=2)
                {
                    b.Box(ridge,V(cx,.51f,cz+side*(sz*.5f-.015f)),V(sx-.04f,.055f,.025f));
                    b.Box(ridge,V(cx+side*(sx*.5f-.015f),.51f,cz),V(.025f,.055f,sz-.04f));
                }
            }

            // Flat metal fragments are sparse and sit below unit feet; no extra cover is implied.
            for(int i=0;i<85;i++)
            {
                float x=2+(float)random.NextDouble()*(width-4),z=2+(float)random.NextDouble()*(height-4);
                if(InBlock(rules,x,z,1.0f)||NearSpawn(rules,x,z,5f))continue;
                float size=.12f+(float)random.NextDouble()*.3f;
                b.Box(i%4==0?p.Dark:dust,V(x,.018f,z),V(size,.025f,size*.55f),V(0,random.Next(180),0));
            }

            // Frame the playable footprint from outside the coordinate bounds.
            b.Box(p.Dark,V(width*.5f,-.01f,-.36f),V(width+.8f,.3f,.3f));
            b.Box(p.Dark,V(width*.5f,-.01f,height+.36f),V(width+.8f,.3f,.3f));
            b.Box(p.Dark,V(-.36f,-.01f,height*.5f),V(.3f,.3f,height+.8f));
            b.Box(p.Dark,V(width+.36f,-.01f,height*.5f),V(.3f,.3f,height+.8f));
            for(int x=0;x<=rules.MapWidth;x+=6)
            {
                BoundaryPost(b,p,boundary,V(x,0,-.48f)); BoundaryPost(b,p,boundary,V(x,0,height+.48f));
            }
            for(int z=3;z<=rules.MapHeight;z+=6)
            {
                BoundaryPost(b,p,boundary,V(-.48f,0,z)); BoundaryPost(b,p,boundary,V(width+.48f,0,z));
            }
            b.Finish();return root;
        }

        static void BoundaryPost(ArtBuilder b,ArtPalette p,Material glow,Vector3 position)
        {
            b.Box(p.Steel,position+V(0,.2f,0),V(.23f,.4f,.23f));
            b.Box(glow,position+V(0,.42f,0),V(.25f,.055f,.25f));
        }
        static bool InBlock(RuleSet rules,float x,float z,float margin)
        {
            if(rules.Blocks==null)return false;
            foreach(var block in rules.Blocks)
                if(x>=block.X-margin&&x<=block.X+block.Width+margin&&z>=block.Z-margin&&z<=block.Z+block.Height+margin)return true;
            return false;
        }
        static bool NearSpawn(RuleSet rules,float x,float z,float radius)
        {
            if(rules.Spawns==null)return false;
            foreach(var spawn in rules.Spawns)
            {
                float dx=x-spawn.Position.X*.001f,dz=z-spawn.Position.Z*.001f;
                if(dx*dx+dz*dz<radius*radius)return true;
            }
            return false;
        }
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    }
}
