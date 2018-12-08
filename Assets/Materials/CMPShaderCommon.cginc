// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

/* CMPShaderCommon.cginc
 *
 * The MIT License (MIT)
 *
 * Copyright (c) 2015 Joseph Riedel
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
*/

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
	OUT.vertex = UnityObjectToClipPos(IN.vertex);
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
