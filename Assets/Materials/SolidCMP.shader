Shader "Custom/SolidCMP" {
	Properties {
		_CMP ("ColorMap", 2D) = "white" {}
		_PAL ("Palette", 2D) = "white" {}
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_LightLevel ("Light Level", Range (0, 31)) = 31
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		Lighting Off
		Blend Off

		Pass
		{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag
		
		#include "UnityCG.cginc"
		#include "Assets/Materials/CMPShaderCommon.cginc"

		sampler2D _MainTex;
		sampler2D _PAL;
		sampler2D _CMP;
		uniform float _LightLevel;

		struct appdata_t
		{
			float4 vertex   : POSITION;
			float2 texcoord : TEXCOORD0;
		};

		struct v2f
		{
			float4 vertex        : POSITION;
			float2 texcoord      : TEXCOORD0;
			float  dist          : TEXCOORD1;
		};

		v2f vert(appdata_t IN)
		{
			v2f OUT;
			OUT.vertex = mul(UNITY_MATRIX_MVP, IN.vertex);
			OUT.texcoord = IN.texcoord;
			OUT.dist = distance(_WorldSpaceCameraPos, mul(_Object2World, IN.vertex));
			return OUT;
		}

		fixed4 frag(v2f IN) : COLOR
		{
			half4 c = CMPShade(_MainTex, _PAL, _CMP, _LightLevel, IN.dist, IN.texcoord);
			return c;
		}

		ENDCG
		}
	} 
}
