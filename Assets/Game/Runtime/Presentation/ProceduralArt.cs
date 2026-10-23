using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColonyRTS.Presentation
{
    public enum ProceduralMeshMode { Shared, Unique }

    /// <summary>Original, procedural RTS silhouettes. Coordinates are local metres, facing +Z.</summary>
    public static class ProceduralArt
    {
        public static int LiveSharedMeshCount => ArtSharedMeshCache.LiveMeshCount;
        public static int LiveSharedMeshReferences => ArtSharedMeshCache.LiveReferences;

        public static GameObject Create(string definitionId, int owner, Transform parent)
            => definitionId=="marine"?CreateInstanced(definitionId,owner,parent):CreateForProfile(definitionId,owner,parent,ProceduralMeshMode.Shared);

        /// <summary>Compact marine rendering; unsupported devices and other definitions retain ordinary renderers.</summary>
        public static GameObject CreateInstanced(string definitionId, int owner, Transform parent)
            => InstancedMarineRenderer.Create(definitionId,owner,parent);

        public static void AccumulateRenderInventory(GameObject root, ArtRenderInventory inventory)
            => ArtRenderInventory.Accumulate(root,inventory);

        /// <summary>Authored part order matches the legacy include-inactive MeshRenderer traversal.</summary>
        public static bool TryGetInstancedPart(GameObject root,int index,out Mesh mesh,out Material material,out Matrix4x4 worldMatrix,out bool active)
        {
            var instance=root!=null?root.GetComponent<InstancedArtInstance>():null;
            if(instance!=null)return instance.TryGetPart(index,out mesh,out material,out worldMatrix,out active);
            mesh=null;material=null;worldMatrix=Matrix4x4.identity;active=false;return false;
        }

        /// <summary>Diagnostics can retain the measured unique-mesh baseline without a mutable global switch.</summary>
        public static GameObject CreateForProfile(string definitionId, int owner, Transform parent, ProceduralMeshMode meshMode)
        {
            if(meshMode!=ProceduralMeshMode.Shared&&meshMode!=ProceduralMeshMode.Unique) throw new ArgumentOutOfRangeException(nameof(meshMode));
            var root = new GameObject("Art_" + definitionId);
            root.transform.SetParent(parent, false);
            var palette = new ArtPalette(owner);
            var body = new ArtBuilder(root.transform, "Hull",meshMode);
            var motion = root.AddComponent<ArtMotion>();
            switch (definitionId)
            {
                case "command": Command(body, palette); break;
                case "barracks": Barracks(body, palette); break;
                case "engineeringbay": EngineeringBay(body, palette); break;
                case "armory": Armory(body, palette); break;
                case "depot": Depot(body, palette); break;
                case "refinery": Refinery(body, palette); break;
                case "turret": Turret(body, palette, motion); break;
                case "worker": Worker(body, palette, motion); break;
                case "marine": Marine(body, palette, motion); break;
                case "mineral": Minerals(body, palette); break;
                case "geyser": Geyser(body, palette); break;
                default: body.Octagon(palette.Armour, Vector3.up * .3f, new Vector3(.5f, .6f, .5f)); break;
            }
            body.Finish();
            return root;
        }

        public static void Animate(GameObject root, string definitionId, bool moving, bool attacking, float time)
        {
            if (root == null) return;
            var rig = root.GetComponent<ArtMotion>();
            Animate(rig,moving,attacking,time);
        }

        internal static void Animate(ArtMotion rig,bool moving,bool attacking,float time)
        {
            if (rig == null) return;
            if(rig.Instanced!=null){rig.Instanced.Animate(moving,attacking,time);return;}
            float stride = moving ? Mathf.Sin(time * 15f) : 0f;
            if (rig.Body != null)
            {
                rig.Body.localPosition = rig.BodyOrigin + Vector3.up * (moving ? Mathf.Abs(stride) * .035f : Mathf.Sin(time * 2.5f) * .009f);
                rig.Body.localRotation = Quaternion.Euler(moving ? 5f : 0f, 0f, moving ? stride * 3f : 0f);
            }
            if (rig.LeftLeg != null && (moving || rig.WasMoving)) rig.LeftLeg.localRotation = Quaternion.Euler(stride * 23f, 0f, 0f);
            if (rig.RightLeg != null && (moving || rig.WasMoving)) rig.RightLeg.localRotation = Quaternion.Euler(-stride * 23f, 0f, 0f);
            if (rig.Tool != null && (attacking || rig.WasAttacking)) rig.Tool.localRotation = Quaternion.Euler(attacking ? -9f + Mathf.Sin(time * 34f) * 4f : 0f, 0f, 0f);
            if (rig.Scanner != null) rig.Scanner.localRotation = Quaternion.Euler(0f, time * 28f, 0f);
            bool flash=attacking && Mathf.Repeat(time * 11f, 1f) < .44f;
            if (rig.Flash != null && flash!=rig.FlashVisible) rig.Flash.SetActive(flash);
            rig.WasAttacking=attacking;rig.WasMoving=moving;rig.FlashVisible=flash;
        }

        static void Command(ArtBuilder b, ArtPalette p)
        {
            b.Octagon(p.Dark, V(0,.14f,0), V(2.35f,.28f,2.35f));
            b.Octagon(p.Steel, V(0,.35f,0), V(2.08f,.22f,2.08f));
            b.Octagon(p.Armour, V(0,.95f,0), V(1.75f,1.12f,1.75f));
            b.Octagon(p.Steel, V(0,1.55f,0), V(1.9f,.22f,1.9f));
            b.Octagon(p.Dark, V(0,1.73f,0), V(1.48f,.16f,1.48f));
            b.Octagon(p.Team, V(0,1.84f,0), V(.93f,.09f,.93f));
            b.Octagon(p.Armour, V(0,1.91f,0), V(.82f,.08f,.82f));
            b.Box(p.White, V(0,1.965f,0), V(.12f,.02f,1.03f));
            b.Box(p.White, V(0,1.965f,0), V(.7f,.02f,.13f));
            for (int i=0;i<8;i++)
            {
                float angle = i * 45f;
                var outward = Quaternion.Euler(0,angle,0);
                b.Box(p.Dark, outward * V(0,.91f,1.65f), V(1.0f,.45f,.13f), V(0,angle,0));
                b.Box(p.Glass, outward * V(0,1.0f,1.73f), V(.83f,.2f,.035f), V(0,angle,0));
                b.Box(p.Glow, outward * V(0,1.27f,1.75f), V(.77f,.035f,.035f), V(0,angle,0));
                b.Box(p.Armour, outward * V(0,.38f,1.99f), V(.46f,.48f,.38f), V(0,angle,0));
                b.Box(p.Team, outward * V(0,.43f,2.19f), V(.29f,.17f,.035f), V(0,angle,0));
            }
            b.Box(p.Dark, V(0,.22f,2.0f), V(1.2f,.18f,.8f));
            b.Box(p.Steel, V(0,.33f,1.96f), V(.95f,.09f,.82f), V(-12,0,0));
            for(int i=0;i<4;i++) b.Box(p.White,V(0,.38f-i*.035f,1.72f+i*.17f),V(.8f,.025f,.045f));
            for(int s=-1;s<=1;s+=2)
            {
                b.Cylinder(p.Dark,V(s*1.55f,1.65f,-.86f),V(.3f,.55f,.3f),8);
                b.Cylinder(p.Steel,V(s*1.55f,1.98f,-.86f),V(.34f,.08f,.34f),8);
                for(int i=0;i<4;i++) b.Box(p.Dark,V(s*1.55f,2.03f,-1.06f+i*.13f),V(.42f,.02f,.04f));
            }
            b.Cylinder(p.Steel,V(.82f,2.3f,.25f),V(.055f,.9f,.055f),6);
            b.Box(p.Team,V(.82f,2.57f,.25f),V(.5f,.17f,.045f),V(0,30,-12));
            b.Sphere(p.Glow,V(.82f,2.79f,.25f),V(.09f,.09f,.09f));
            b.Box(p.Warning,V(-.67f,1.98f,-.62f),V(.55f,.025f,.18f));
        }

        static void Barracks(ArtBuilder b, ArtPalette p)
        {
            b.Box(p.Dark,V(0,.12f,0),V(3.25f,.24f,3.2f));
            b.Box(p.Steel,V(0,.35f,0),V(2.9f,.27f,2.8f));
            b.Box(p.Armour,V(0,.96f,-.2f),V(2.55f,1.25f,2.35f));
            b.Box(p.Steel,V(0,1.66f,-.2f),V(2.8f,.2f,2.62f));
            b.Box(p.Dark,V(0,.89f,1.0f),V(1.53f,1.12f,.055f));
            for(int i=0;i<6;i++) b.Box(p.Steel,V(0,.45f+i*.15f,1.045f),V(1.42f,.1f,.055f));
            b.Box(p.Team,V(0,1.5f,1.06f),V(1.62f,.14f,.07f));
            b.Box(p.Glow,V(0,1.6f,1.09f),V(1.48f,.025f,.03f));
            b.Box(p.Steel,V(0,.19f,1.48f),V(1.7f,.14f,.95f),V(-8,0,0));
            for(int s=-1;s<=1;s+=2)
            {
                b.Box(p.Steel,V(s*1.26f,.95f,.6f),V(.24f,1.45f,.31f));
                b.Box(p.Team,V(s*1.4f,1.0f,-.3f),V(.035f,.47f,.85f));
                b.Box(p.Dark,V(s*1.3f,.94f,-.85f),V(.12f,.59f,.52f));
                for(int j=0;j<4;j++) b.Box(p.Steel,V(s*1.38f,.75f+j*.13f,-.85f),V(.03f,.06f,.45f));
                b.Box(p.Warning,V(s*.9f,.3f,1.63f),V(.1f,.025f,.5f),V(0,s*15,0));
            }
            b.Box(p.Dark,V(.63f,1.81f,-.47f),V(.84f,.15f,1.3f));
            for(int i=0;i<6;i++) b.Box(p.Steel,V(.63f,1.91f,-.98f+i*.2f),V(.73f,.06f,.07f));
            b.Box(p.Team,V(-.65f,1.8f,-.19f),V(.4f,.05f,1.65f));
            b.Cylinder(p.Steel,V(-.8f,2.0f,-1.0f),V(.12f,.55f,.12f),8);
            b.Cylinder(p.Dark,V(-.8f,2.3f,-1.0f),V(.16f,.08f,.16f),8);
            b.Sphere(p.Glow,V(1.25f,1.8f,1.03f),V(.085f,.085f,.085f));
        }

        static void EngineeringBay(ArtBuilder b, ArtPalette p)
        {
            b.Box(p.Dark,V(0,.12f,0),V(3.15f,.24f,2.75f));
            b.Box(p.Steel,V(-.3f,.36f,-.15f),V(2.45f,.25f,2.3f));
            b.Box(p.Armour,V(-.3f,.84f,-.4f),V(2.2f,.86f,1.75f));
            b.Box(p.Team,V(-.3f,1.33f,-.4f),V(2.33f,.14f,1.9f));
            b.Box(p.Dark,V(-.3f,.75f,.5f),V(1.3f,.55f,.04f));
            for(int i=0;i<5;i++) b.Box(p.Steel,V(-.3f,.53f+i*.11f,.54f),V(1.21f,.055f,.045f));
            b.Box(p.Glow,V(-.3f,1.11f,.55f),V(1.2f,.035f,.03f));
            b.Cylinder(p.Dark,V(.96f,.55f,.6f),V(.57f,.76f,.57f),10);
            b.Cylinder(p.Steel,V(.96f,.98f,.6f),V(.66f,.12f,.66f),10);
            b.Cylinder(p.Glow,V(.96f,1.06f,.6f),V(.46f,.055f,.46f),10);
            b.Cylinder(p.Dark,V(.96f,1.1f,.6f),V(.35f,.04f,.35f),10);
            b.Box(p.Warning,V(-1.34f,1.18f,.66f),V(.16f,1.83f,.18f));
            b.Box(p.Warning,V(-.62f,2.13f,.66f),V(1.6f,.2f,.23f));
            b.Cylinder(p.Steel,V(.02f,1.85f,.66f),V(.035f,.45f,.035f),6);
            b.Box(p.Dark,V(.02f,1.61f,.66f),V(.19f,.12f,.19f));
            b.Cylinder(p.Steel,V(-.42f,1.6f,-.55f),V(.085f,.44f,.085f),6);
            b.Cylinder(p.Dark,V(-.42f,1.88f,-.55f),V(.6f,.12f,.6f),10,V(18,0,0));
            b.Cylinder(p.Steel,V(-.42f,1.94f,-.55f),V(.49f,.09f,.49f),10,V(18,0,0));
            b.Box(p.Glow,V(-.42f,2.02f,-.55f),V(.11f,.19f,.11f));
            for(int i=0;i<4;i++) b.Box(p.Dark,V(-1.42f,.72f,-.94f+i*.26f),V(.045f,.3f,.12f));
            b.Box(p.White,V(.5f,1.42f,-.78f),V(.12f,.025f,.45f));
            b.Box(p.White,V(.5f,1.42f,-.78f),V(.45f,.025f,.12f));
        }

        static void Armory(ArtBuilder b, ArtPalette p)
        {
            b.Octagon(p.Dark,V(0,.14f,0),V(1.94f,.28f,1.75f));
            b.Box(p.Steel,V(0,.38f,0),V(3.2f,.28f,2.6f));
            b.Box(p.Armour,V(0,1.04f,-.06f),V(2.7f,1.18f,2.25f));
            b.Box(p.Dark,V(0,1.72f,-.06f),V(2.85f,.19f,2.45f));
            b.Box(p.Team,V(0,1.85f,-.12f),V(2.59f,.12f,2.22f));
            b.Box(p.Dark,V(0,.93f,1.1f),V(1.27f,.99f,.07f));
            for(int side=-1;side<=1;side+=2)
            {
                b.Box(p.Steel,V(side*.33f,.94f,1.16f),V(.56f,.88f,.12f));
                b.Box(p.Team,V(side*.33f,.94f,1.24f),V(.19f,.63f,.045f));
                b.Box(p.Warning,V(side*.56f,.54f,1.25f),V(.09f,.12f,.03f));
                b.Box(p.Armour,V(side*1.39f,1.17f,-.15f),V(.42f,1.82f,1.4f));
                b.Box(p.Steel,V(side*1.39f,2.12f,-.15f),V(.5f,.13f,1.5f));
                b.Box(p.Glow,V(side*1.62f,1.29f,-.15f),V(.025f,.7f,.18f));
                for(int i=0;i<5;i++) b.Box(p.Dark,V(side*1.62f,.54f+i*.14f,.33f),V(.03f,.06f,.3f));
                b.Cylinder(p.Steel,V(side*.7f,2.05f,-.58f),V(.27f,.34f,.27f),8);
                b.Cylinder(p.Dark,V(side*.7f,2.27f,-.58f),V(.3f,.09f,.3f),8);
            }
            b.Box(p.Glow,V(0,1.49f,1.19f),V(.95f,.045f,.03f));
            b.Box(p.Dark,V(0,1.95f,.18f),V(.63f,.13f,1.1f));
            for(int i=0;i<4;i++) b.Box(p.Steel,V(0,2.04f,-.22f+i*.25f),V(.54f,.06f,.07f));
            b.Box(p.White,V(0,1.84f,1.04f),V(.18f,.03f,.25f));
            b.Box(p.Steel,V(0,.27f,1.47f),V(1.35f,.11f,.7f),V(-8,0,0));
        }

        static void Depot(ArtBuilder b, ArtPalette p)
        {
            b.Box(p.Dark,V(0,.12f,0),V(2.25f,.24f,1.8f));
            for(int s=-1;s<=1;s+=2)
            {
                b.Box(p.Armour,V(s*.49f,.67f,0),V(.88f,.98f,1.52f));
                b.Box(p.Team,V(s*.49f,1.19f,0),V(.7f,.09f,1.35f));
                b.Box(p.Steel,V(s*.98f,.69f,0),V(.1f,1.08f,1.72f));
                for(int j=0;j<4;j++) b.Box(p.Steel,V(s*.49f,.68f,-.59f+j*.4f),V(.97f,1.0f,.075f));
                b.Box(p.Glow,V(s*.49f,.75f,.81f),V(.43f,.1f,.035f));
            }
            for(int z=-1;z<=1;z+=2) b.Box(p.Steel,V(0,.3f,z*.87f),V(2.22f,.22f,.16f));
            b.Box(p.Warning,V(0,1.2f,0),V(.15f,.025f,1.4f));
        }

        static void Refinery(ArtBuilder b, ArtPalette p)
        {
            b.Octagon(p.Dark,V(0,.16f,0),V(1.65f,.32f,1.65f));
            b.Cylinder(p.Steel,V(0,.84f,0),V(.8f,1.35f,.8f),10);
            b.Cylinder(p.Team,V(0,1.17f,0),V(.85f,.24f,.85f),10);
            b.Cylinder(p.Steel,V(0,1.63f,0),V(.98f,.23f,.98f),10);
            b.Cylinder(p.Gas,V(0,1.9f,0),V(.55f,.34f,.55f),10);
            b.Cylinder(p.Dark,V(0,2.13f,0),V(.73f,.16f,.73f),10);
            for(int i=0;i<4;i++)
            {
                float a=i*90f+45f; var q=Quaternion.Euler(0,a,0);
                b.Cylinder(p.Armour,q*V(0,.62f,1.13f),V(.34f,1.06f,.34f),8);
                b.Cylinder(p.Gas,q*V(0,.83f,1.13f),V(.35f,.2f,.35f),8);
                b.Box(p.Steel,q*V(0,.37f,.83f),V(.24f,.24f,.72f),V(0,a,0));
            }
            b.Box(p.Armour,V(1.12f,.8f,.6f),V(.65f,.7f,.6f));
            b.Box(p.Glow,V(1.12f,1.17f,.6f),V(.42f,.025f,.25f));
        }

        static void Turret(ArtBuilder b, ArtPalette p, ArtMotion rig)
        {
            b.Octagon(p.Dark,V(0,.12f,0),V(.85f,.24f,.85f));
            for(int i=0;i<4;i++) b.Box(p.Steel,Quaternion.Euler(0,i*90,0)*V(0,.22f,.65f),V(.24f,.25f,.7f),V(0,i*90,0));
            b.Cylinder(p.Armour,V(0,.53f,0),V(.4f,.7f,.4f),8);
            var pivot=Child(b.Parent,"Weapon",V(0,.88f,0)); rig.Tool=pivot;
            var gun = new ArtBuilder(pivot,"WeaponMeshes",b.MeshMode);
            gun.Box(p.Team,V(0,.13f,0),V(.85f,.31f,.66f));
            for(int s=-1;s<=1;s+=2)
            {
                gun.Cylinder(p.Steel,V(s*.27f,.19f,.45f),V(.09f,.9f,.09f),8,V(90,0,0));
                gun.Cylinder(p.Dark,V(s*.27f,.19f,.93f),V(.12f,.13f,.12f),8,V(90,0,0));
            }
            gun.Box(p.Glow,V(0,.2f,.38f),V(.17f,.11f,.035f)); gun.Finish();
            Flash(rig,pivot,p,V(0,.19f,1.05f),.24f,b.MeshMode);
        }

        static void Marine(ArtBuilder b, ArtPalette p, ArtMotion rig)
        {
            rig.LeftLeg=Child(b.Parent,"LeftLeg",V(-.18f,.4f,0));
            rig.RightLeg=Child(b.Parent,"RightLeg",V(.18f,.4f,0));
            foreach(var leg in new[]{rig.LeftLeg,rig.RightLeg})
            {
                var l=new ArtBuilder(leg,"LegMeshes",b.MeshMode);
                l.Box(p.Dark,V(0,-.12f,0),V(.19f,.25f,.2f));
                l.Box(p.Armour,V(0,-.23f,.02f),V(.24f,.24f,.25f));
                l.Box(p.Steel,V(0,-.34f,.08f),V(.27f,.13f,.37f)); l.Finish();
            }
            rig.Body=Child(b.Parent,"Body",V(0,.72f,0)); rig.BodyOrigin=rig.Body.localPosition;
            var torso=new ArtBuilder(rig.Body,"TorsoMeshes",b.MeshMode);
            torso.Octagon(p.Armour,V(0,0,0),V(.31f,.45f,.24f));
            torso.Box(p.Team,V(0,.05f,.23f),V(.42f,.24f,.09f));
            torso.Box(p.Dark,V(0,0,-.25f),V(.32f,.35f,.18f));
            torso.Box(p.Glow,V(0,.13f,-.35f),V(.16f,.06f,.025f));
            for(int s=-1;s<=1;s+=2)
            {
                torso.Sphere(p.Team,V(s*.36f,.14f,0),V(.2f,.18f,.23f));
                torso.Box(p.Armour,V(s*.38f,-.1f,.1f),V(.17f,.3f,.19f),V(-15,0,s*12));
                torso.Box(p.White,V(s*.4f,.3f,0),V(.12f,.025f,.18f));
            }
            torso.Sphere(p.Dark,V(0,.37f,.04f),V(.225f,.235f,.21f));
            torso.Box(p.Team,V(0,.44f,-.04f),V(.39f,.13f,.27f));
            torso.Box(p.Glass,V(0,.37f,.225f),V(.33f,.105f,.035f));
            torso.Box(p.Glow,V(0,.42f,.243f),V(.27f,.019f,.02f)); torso.Finish();
            rig.Tool=Child(rig.Body,"Rifle",V(.2f,-.05f,.28f));
            var gun=new ArtBuilder(rig.Tool,"RifleMeshes",b.MeshMode);
            gun.Box(p.Dark,V(0,0,.12f),V(.15f,.16f,.65f));
            gun.Box(p.Steel,V(0,.11f,.05f),V(.12f,.08f,.4f));
            gun.Cylinder(p.Steel,V(0,0,.5f),V(.05f,.22f,.05f),6,V(90,0,0));
            gun.Box(p.Team,V(0,-.04f,-.05f),V(.17f,.2f,.19f)); gun.Finish();
            Flash(rig,rig.Tool,p,V(0,0,.66f),.19f,b.MeshMode);
        }

        static void Worker(ArtBuilder b, ArtPalette p, ArtMotion rig)
        {
            for(int s=-1;s<=1;s+=2)
            {
                var leg=Child(b.Parent,s<0?"LeftLeg":"RightLeg",V(s*.3f,.36f,0));
                if(s<0) rig.LeftLeg=leg; else rig.RightLeg=leg;
                var l=new ArtBuilder(leg,"MechFoot",b.MeshMode);
                l.Box(p.Steel,V(0,-.08f,0),V(.21f,.28f,.24f));
                l.Box(p.Dark,V(0,-.27f,.06f),V(.36f,.15f,.53f));
                l.Box(p.Warning,V(0,-.18f,.23f),V(.25f,.05f,.08f)); l.Finish();
            }
            rig.Body=Child(b.Parent,"Body",V(0,.75f,0)); rig.BodyOrigin=rig.Body.localPosition;
            var body=new ArtBuilder(rig.Body,"MechHull",b.MeshMode);
            body.Octagon(p.Armour,V(0,0,0),V(.43f,.6f,.34f));
            body.Box(p.Team,V(0,.19f,-.02f),V(.66f,.21f,.51f));
            body.Box(p.Glass,V(0,.1f,.33f),V(.5f,.28f,.055f),V(-12,0,0));
            body.Box(p.Steel,V(0,-.13f,.35f),V(.59f,.12f,.13f));
            body.Box(p.Glow,V(0,.25f,.39f),V(.36f,.035f,.025f));
            for(int s=-1;s<=1;s+=2)
            {
                body.Cylinder(p.Steel,V(s*.3f,.1f,-.38f),V(.12f,.63f,.12f),6);
                body.Box(p.Dark,V(s*.39f,.04f,.03f),V(.21f,.3f,.25f));
            }
            body.Box(p.Warning,V(0,.32f,-.05f),V(.4f,.025f,.2f)); body.Finish();
            rig.Tool=Child(rig.Body,"ToolArm",V(0,-.07f,.24f));
            var arms=new ArtBuilder(rig.Tool,"ClawMeshes",b.MeshMode);
            for(int s=-1;s<=1;s+=2)
            {
                arms.Box(p.Team,V(s*.48f,0,.14f),V(.19f,.24f,.4f),V(0,s*-16,0));
                arms.Box(p.Steel,V(s*.43f,-.05f,.43f),V(.14f,.19f,.32f),V(0,s*21,0));
                arms.Box(p.Dark,V(s*.32f,-.02f,.59f),V(.24f,.12f,.1f));
            }
            arms.Finish(); Flash(rig,rig.Tool,p,V(.31f,.02f,.66f),.11f,b.MeshMode);
        }

        static void Minerals(ArtBuilder b, ArtPalette p)
        {
            b.Octagon(p.Rock,V(0,.09f,0),V(.88f,.18f,.66f));
            for(int i=0;i<7;i++)
            {
                float a=i*2.399963f;
                float r=i==0?0f:.25f+(i%3)*.15f;
                float h=1.25f-(i%4)*.18f;
                b.Crystal(i%2==0?p.Crystal:p.CrystalLight,V(Mathf.Sin(a)*r,h*.49f+.13f,Mathf.Cos(a)*r),V(.24f,h,.24f),V((i%3-1)*17,i*47,(i%2==0?1:-1)*13));
            }
        }

        static void Geyser(ArtBuilder b, ArtPalette p)
        {
            for(int i=0;i<7;i++)
            {
                float a=i*Mathf.PI*2f/7f;
                b.Octagon(p.Rock,V(Mathf.Sin(a)*.65f,.3f,Mathf.Cos(a)*.65f),V(.46f,.6f+(i%3)*.16f,.4f),V(0,i*27,0));
            }
            b.Cylinder(p.Dark,V(0,.27f,0),V(.5f,.2f,.5f),9);
            b.Cylinder(p.Gas,V(0,.39f,0),V(.38f,.05f,.38f),9);
            b.Crystal(p.Gas,V(-.15f,.66f,.1f),V(.19f,.48f,.19f),V(5,20,-15));
            b.Crystal(p.Gas,V(.19f,.57f,-.09f),V(.14f,.34f,.14f),V(-9,50,12));
        }

        static void Flash(ArtMotion rig, Transform parent, ArtPalette p, Vector3 position, float size, ProceduralMeshMode meshMode)
        {
            var flash=Child(parent,"MuzzleFlash",position); var f=new ArtBuilder(flash,"FlashMeshes",meshMode);
            f.Crystal(p.Flash,Vector3.zero,V(size,size*.6f,size*1.9f),V(90,0,0));
            f.Finish(); rig.Flash=flash.gameObject; rig.Flash.SetActive(false);
        }

        internal static Transform Child(Transform parent,string name,Vector3 position)
        {
            var child=new GameObject(name).transform; child.SetParent(parent,false); child.localPosition=position; return child;
        }
        internal static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    }

    // Visual state is attached to its GameObject, so destroying an entity also releases its rig.
    public sealed class ArtMotion : MonoBehaviour
    {
        internal Transform Body, LeftLeg, RightLeg, Tool, Scanner;
        internal Vector3 BodyOrigin;
        internal GameObject Flash;
        internal InstancedArtInstance Instanced;
        internal bool WasAttacking, WasMoving, FlashVisible;
    }

    internal sealed class ArtPalette
    {
        public readonly Material Dark,Steel,Armour,Team,Glow,Glass,White,Warning,Rock,Gas,Crystal,CrystalLight,Flash;
        static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        public ArtPalette(int owner)
        {
            Color team=owner==0?new Color(.08f,.68f,.86f):owner==1?new Color(.85f,.18f,.12f):new Color(.54f,.56f,.54f);
            Dark=Get("dark",new Color(.075f,.105f,.13f),.5f);
            Steel=Get("steel",new Color(.32f,.39f,.42f),.65f);
            Armour=Get("armour",new Color(.22f,.29f,.31f),.45f);
            Team=Get("team"+owner,team,.35f);
            Glow=Get("glow"+owner,team*.8f,.3f,team*1.7f);
            Glass=Get("glass"+owner,Color.Lerp(new Color(.025f,.1f,.16f),team,.36f),.7f,team*.3f);
            White=Get("white",new Color(.74f,.8f,.76f),.2f);
            Warning=Get("warning",new Color(.94f,.56f,.1f),.35f);
            Rock=Get("rock",new Color(.29f,.26f,.22f),.03f);
            Gas=Get("gas",new Color(.2f,.7f,.27f),.2f,new Color(.23f,.66f,.24f));
            Crystal=Get("crystal",new Color(.035f,.45f,.74f),.45f,new Color(.03f,.3f,.6f));
            CrystalLight=Get("crystal-light",new Color(.18f,.73f,.9f),.4f,new Color(.08f,.37f,.48f));
            Flash=Get("flash",new Color(1f,.85f,.39f),0,new Color(2f,1.4f,.3f));
        }
        internal static Material Get(string key,Color color,float metallic=0f,Color emission=default)
        {
            if(Materials.TryGetValue(key,out var existing) && existing!=null) return existing;
            var shader=Shader.Find("Standard");
            if(shader==null) shader=Shader.Find("Legacy Shaders/Diffuse");
            var material=new Material(shader){name="RTS_"+key,color=color,enableInstancing=true};
            if(material.HasProperty("_Metallic")) material.SetFloat("_Metallic",metallic);
            if(material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness",metallic>.3f?.42f:.16f);
            if(emission.maxColorComponent>0f)
            {
                material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",emission);
            }
            Materials[key]=material; return material;
        }
    }

    /// <summary>Combines each material batch into one mesh; creates no colliders or gameplay objects.</summary>
    internal sealed class ArtBuilder
    {
        internal readonly Transform Parent;
        internal readonly ProceduralMeshMode MeshMode;
        readonly string name;
        readonly Dictionary<Material,List<CombineInstance>> parts=new Dictionary<Material,List<CombineInstance>>();
        static readonly Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
        internal ArtBuilder(Transform parent,string name,ProceduralMeshMode meshMode=ProceduralMeshMode.Shared)
        {Parent=parent;this.name=name;MeshMode=meshMode;}
        internal void Box(Material mat,Vector3 position,Vector3 size,Vector3 rotation=default)=>Add(mat,Shape("box"),position,size,rotation);
        internal void Octagon(Material mat,Vector3 position,Vector3 size,Vector3 rotation=default)=>Cylinder(mat,position,size,8,rotation);
        internal void Cylinder(Material mat,Vector3 position,Vector3 size,int sides,Vector3 rotation=default)=>Add(mat,Shape("cylinder"+sides,sides),position,size,rotation);
        internal void Crystal(Material mat,Vector3 position,Vector3 size,Vector3 rotation=default)=>Add(mat,Shape("crystal",5,true),position,size,rotation);
        internal void Sphere(Material mat,Vector3 position,Vector3 size)=>Add(mat,Shape("sphere",8),position,size,Vector3.zero);
        void Add(Material mat,Mesh mesh,Vector3 pos,Vector3 scale,Vector3 rotation)
        {
            if(!parts.TryGetValue(mat,out var list)){list=new List<CombineInstance>();parts.Add(mat,list);}
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(pos,Quaternion.Euler(rotation),scale)});
        }
        internal void Finish()
        {
            foreach(var batch in parts)
            {
                var child=new GameObject(name+"_"+batch.Key.name);
                // Register the lifetime while active, even if its eventual parent is
                // inactive; Unity skips OnDestroy for components that never awakened.
                var lifetime=child.AddComponent<ArtMeshLifetime>();
                child.transform.SetParent(Parent,false);
                var instances=batch.Value.ToArray();
                Mesh mesh;
                if(MeshMode==ProceduralMeshMode.Shared)
                {
                    lifetime.SharedLease=ArtSharedMeshCache.Acquire(instances,child.name);
                    mesh=lifetime.SharedLease.Mesh;
                }
                else mesh=ArtSharedMeshCache.CreateMesh(instances,child.name);
                lifetime.Mesh=mesh;
                child.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=child.AddComponent<MeshRenderer>(); renderer.sharedMaterial=batch.Key;
                renderer.shadowCastingMode=ShadowCastingMode.On; renderer.receiveShadows=true;
            }
            parts.Clear();
        }
        static Mesh Shape(string key,int sides=4,bool crystal=false)
        {
            if(Meshes.TryGetValue(key,out var existing)&&existing!=null)return existing;
            var vertices=new List<Vector3>(); var indices=new List<int>();
            if(key=="box")
            {
                var v=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f),new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
                Quad(vertices,indices,v[4],v[5],v[1],v[0]); Quad(vertices,indices,v[6],v[7],v[3],v[2]);
                Quad(vertices,indices,v[7],v[4],v[0],v[3]); Quad(vertices,indices,v[5],v[6],v[2],v[1]);
                Quad(vertices,indices,v[7],v[6],v[5],v[4]); Quad(vertices,indices,v[0],v[1],v[2],v[3]);
            }
            else if(key=="sphere")
            {
                for(int y=0;y<5;y++) for(int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2f/sides,c=(i+1)*Mathf.PI*2f/sides;
                    float t=-Mathf.PI*.5f+y*Mathf.PI/5f,u=-Mathf.PI*.5f+(y+1)*Mathf.PI/5f;
                    Quad(vertices,indices,Spherical(a,t),Spherical(c,t),Spherical(c,u),Spherical(a,u));
                }
            }
            else
            {
                for(int i=0;i<sides;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2f/sides,c=(i+1.5f)*Mathf.PI*2f/sides;
                    Vector3 loA=new Vector3(Mathf.Sin(a),crystal?-.28f:-.5f,Mathf.Cos(a));
                    Vector3 loB=new Vector3(Mathf.Sin(c),crystal?-.28f:-.5f,Mathf.Cos(c));
                    if(crystal)
                    {
                        Triangle(vertices,indices,loA,loB,Vector3.up*.5f);
                        Triangle(vertices,indices,loB,loA,Vector3.down*.5f);
                    }
                    else
                    {
                        var hiA=new Vector3(loA.x,.5f,loA.z);var hiB=new Vector3(loB.x,.5f,loB.z);
                        Quad(vertices,indices,loA,loB,hiB,hiA);
                        Triangle(vertices,indices,hiA,hiB,Vector3.up*.5f);
                        Triangle(vertices,indices,loB,loA,Vector3.down*.5f);
                    }
                }
            }
            var result=new Mesh{name="RTS_"+key}; result.SetVertices(vertices);result.SetTriangles(indices,0);result.RecalculateNormals();result.RecalculateBounds();
            Meshes[key]=result;return result;
        }
        static Vector3 Spherical(float a,float t)=>new Vector3(Mathf.Sin(a)*Mathf.Cos(t),Mathf.Sin(t),Mathf.Cos(a)*Mathf.Cos(t));
        static void Quad(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=v.Count;v.Add(a);v.Add(b);v.Add(c);v.Add(d);t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
        }
        static void Triangle(List<Vector3> v,List<int> t,Vector3 a,Vector3 b,Vector3 c)
        {
            int n=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(n);t.Add(n+1);t.Add(n+2);
        }
    }

    // Derived meshes exist only while actual renderers lease them. Exact geometry,
    // not owner/material identity, forms the key, allowing both palettes to instance.
    internal static class ArtSharedMeshCache
    {
        internal sealed class GeometryKey : IEquatable<GeometryKey>
        {
            readonly CombineInstance[] parts;
            readonly int hash;
            internal GeometryKey(CombineInstance[] parts)
            {
                this.parts=parts;
                unchecked
                {
                    int value=17;
                    foreach(var part in parts)
                    {
                        value=value*31+part.mesh.GetInstanceID();
                        value=value*31+part.subMeshIndex;
                        value=value*31+part.transform.GetHashCode();
                    }
                    hash=value;
                }
            }
            public bool Equals(GeometryKey other)
            {
                if(ReferenceEquals(this,other))return true;
                if(other==null||hash!=other.hash||parts.Length!=other.parts.Length)return false;
                for(int i=0;i<parts.Length;i++)
                    if(parts[i].mesh!=other.parts[i].mesh||parts[i].subMeshIndex!=other.parts[i].subMeshIndex||
                        !parts[i].transform.Equals(other.parts[i].transform))return false;
                return true;
            }
            public override bool Equals(object other)=>other is GeometryKey key&&Equals(key);
            public override int GetHashCode()=>hash;
        }
        internal sealed class Entry
        {
            internal readonly GeometryKey Key;
            internal readonly Mesh Mesh;
            internal int References;
            internal Entry(GeometryKey key,Mesh mesh){Key=key;Mesh=mesh;}
        }
        static readonly Dictionary<GeometryKey,Entry> entries=new Dictionary<GeometryKey,Entry>();
        internal static int LiveMeshCount=>entries.Count;
        internal static int LiveReferences{get;private set;}

        internal static Entry Acquire(CombineInstance[] parts,string name)
        {
            var key=new GeometryKey(parts);
            if(!entries.TryGetValue(key,out var entry))
            {
                entry=new Entry(key,CreateMesh(parts,name));
                entries.Add(key,entry);
            }
            if(entry.Mesh==null)throw new InvalidOperationException("A procedural mesh was destroyed while live renderers still leased it.");
            entry.References++;LiveReferences++;return entry;
        }
        internal static void Retain(Entry entry)
        {
            if(entry==null||entry.References<=0||entry.Mesh==null)throw new InvalidOperationException("Cannot retain released procedural geometry.");
            entry.References++;LiveReferences++;
        }
        internal static void Release(Entry entry)
        {
            if(entry==null||entry.References<=0)return;
            entry.References--;LiveReferences--;
            if(entry.References>0)return;
            entries.Remove(entry.Key);
            DestroyMesh(entry.Mesh);
        }
        internal static Mesh CreateMesh(CombineInstance[] parts,string name)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
            try{mesh.CombineMeshes(parts,true,true);mesh.RecalculateBounds();return mesh;}
            catch{DestroyMesh(mesh);throw;}
        }
        internal static void DestroyMesh(Mesh mesh)
        {
            if(mesh==null)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);
            else UnityEngine.Object.DestroyImmediate(mesh);
        }
    }

    [ExecuteAlways]
    public sealed class ArtMeshLifetime : MonoBehaviour
    {
        internal Mesh Mesh;
        internal ArtSharedMeshCache.Entry SharedLease;
        // Generated previews also own native meshes outside Play Mode. Explicit
        // registration plus ExecuteAlways makes their destruction an editor lifecycle
        // event as well; no Update or edit-time gameplay work is introduced.
        void Awake() { }
        void OnDestroy()
        {
            if(SharedLease!=null){ArtSharedMeshCache.Release(SharedLease);SharedLease=null;}
            else ArtSharedMeshCache.DestroyMesh(Mesh);
            Mesh=null;
        }
    }
}
