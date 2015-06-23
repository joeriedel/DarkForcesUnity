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
	float4 clip     : POSITION1;
};

half4 CMPPixel(sampler2D main, sampler2D pal, sampler2D cmp, float lightLevel, float dist, float2 uv) {
	half c = tex2D(main, uv).a;

	const float palOX = 0;
	const float palOY = 0;
	const float cmpOX = 0;
	const float cmpOY = 0;
	
	uv.x = cmpOX + c;
	uv.y = cmpOY + (clamp(lightLevel - (dist/8), 0, 31) / 33.0);

	c = tex2D(cmp, uv).a;

	uv.x = palOX + c;
	uv.y = palOY;

	half4 z = tex2D(pal, uv);
	return z;
}

v2f CMPVert(appdata_t IN)
{
	v2f OUT;
	OUT.vertex = mul(UNITY_MATRIX_MVP, IN.vertex);
	OUT.texcoord = IN.texcoord;
	OUT.clip = OUT.vertex;
	return OUT;
}

float4 CMPFrag(v2f IN) : COLOR
{
	float z = lerp(_ProjectionParams.y, _ProjectionParams.z, Linear01Depth(IN.clip.z/IN.clip.w));
	half4 c = CMPPixel(_MainTex, _PAL, _CMP, _LightLevel, z, IN.texcoord);
	return c;
}
