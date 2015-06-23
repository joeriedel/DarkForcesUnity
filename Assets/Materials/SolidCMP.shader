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
		#pragma vertex CMPVert
		#pragma fragment CMPFrag
		#pragma target 3.0

		#include "UnityCG.cginc"
		#include "Assets/Materials/CMPShaderCommon.cginc"
				
		ENDCG
		}
	} 
}
