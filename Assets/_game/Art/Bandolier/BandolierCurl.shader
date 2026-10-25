Shader "Game/Bandolier Curl"
{
    Properties
    {
        _MainTex ("Layer texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BackColor ("Leather underside", Color) = (.78,.63,.48,1)
        _Progress ("Unrolled", Range(0,1)) = 1
        _Width ("Sheet width", Float) = 12.6
        _Radius ("Roll radius", Float) = .78
        _Thickness ("Spiral spacing", Float) = .006
        _AnchorX ("Layer horizontal anchor", Float) = 0
        _Rigid ("Rigid cartridge", Float) = 0
        _BendAngle ("Cartridge rotation", Float) = 0
        _Shadow ("Projected contact shadow", Float) = 0
        _Gesture ("Throw timeline", Range(0,1)) = 1
        _ImpactStrength ("Leather impact", Float) = 1
        [Enum(Off,0,On,1)] _ZWrite ("Write depth", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        ZWrite [_ZWrite]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _Color, _BackColor;
            float _Progress, _Width, _Radius, _Thickness, _AnchorX, _Rigid, _BendAngle, _Shadow, _Gesture, _ImpactStrength;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float angle:TEXCOORD1; };
            float3 slap(float x)
            {
                float u=saturate((x+_Width*.5)/_Width);
                float age=_Gesture-(.09+.30*pow(u,1.2));
                if(age<=0||_Gesture>=.9999)return 0;
                float damp=exp(-age*11);
                float height=-abs(sin(age*48))*damp*(.20+.30*u)*_ImpactStrength;
                float side=sin(age*57)*damp*.065*_ImpactStrength;
                float slide=sin(age*39+u*2)*damp*.075*_ImpactStrength;
                return float3(side,slide,height);
            }
            v2f vert(appdata v)
            {
                v2f o; float3 p=v.vertex.xyz; float angle=_BendAngle;
                if (_Rigid < .5)
                {
                    p.x += _AnchorX;
                    float front=saturate(_Progress)*_Width;
                    float d=p.x+_Width*.5-front;
                    angle=0;
                    float originalX=p.x;
                    float3 impact=slap(originalX);
                    if (d > 0)
                    {
                        float radius=min(_Radius,max(.04,(_Width-front)/4.2));
                        float k=max(.00001,min(_Thickness,radius*.005));
                        angle=(radius-sqrt(max(.000001,radius*radius-2*k*d)))/k;
                        float r=radius-k*angle;
                        float offset=p.z;
                        p.x=-_Width*.5+front+r*sin(angle)+offset*sin(angle);
                        p.z=-radius+r*cos(angle)+offset*cos(angle);
                    }
                    // Waves run along the already released leather, not through the inner coil.
                    float released=1-smoothstep(-.25,.15,d);
                    p+=impact*released;
                    float gradient=(slap(originalX+.025).z-slap(originalX-.025).z)/.05;
                    angle+=atan(gradient)*released;
                }
                if (_Shadow > .5)
                {
                    p.x-=p.z*.24; p.y-=.07; p.z=.14;
                }
                o.vertex=UnityObjectToClipPos(float4(p,1));o.uv=v.uv;o.angle=angle;return o;
            }
            fixed4 frag(v2f i, fixed facing:VFACE):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*_Color;
                clip(c.a-.09);
                if (_Shadow > .5) return fixed4(.02,.01,.005,c.a*.38);
                float light=.32+.68*sqrt(abs(cos(i.angle)));
                light+=.12*pow(abs(cos(i.angle+.45)),18);
                // The back is an unprinted, darker leather surface; cartridges stay metallic.
                if (facing < 0 && _Rigid < .5) c.rgb=lerp(c.rgb,c.rgb*_BackColor.rgb,.85);
                c.rgb*=light;
                return c;
            }
            ENDCG
        }
    }
}
