half4 CMPShade(sampler2D main, sampler2D pal, sampler2D cmp, float lightLevel, float dist, float2 uv) {
	half c = tex2D(main, uv).a;

	const float palOX = 0;
	const float palOY = 0;
	const float cmpOX = 0;
	const float cmpOY = 0;
	
	uv.x = cmpOX + c;
	uv.y = cmpOY + (clamp(lightLevel - (dist/16), 0, 31) / 33.0);

	c = tex2D(cmp, uv).a;

	uv.x = palOX + c;
	uv.y = palOY;

	half4 z = tex2D(pal, uv);
	return z;
}
