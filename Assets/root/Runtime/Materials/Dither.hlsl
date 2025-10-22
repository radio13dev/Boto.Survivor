#ifndef DITHER_INCLUDED
#define DITHER_INCLUDED

void Dither_float(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, out float dither){
	int2 checker = frac(screenPosition) > 0.5;
	dither = checker.x ^ checker.y ? 1 : 0;
	return;
	
	float2 floord = abs(floor(screenPosition));
	float ditherValue = (floord.x + floord.y)%2;
	dither = ditherValue;
}
void Dither_half(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, out float dither){
	int2 checker = frac(screenPosition) > 0.5;
	dither = checker.x ^ checker.y ? 1 : 0;
	return;
	
	float2 floord = abs(floor(screenPosition));
	float ditherValue = (floord.x + floord.y)%2;
	dither = ditherValue;
}
void DitherCustom_float(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, float4 ditherMods, out float dither){
	float2 floord = abs(floor(screenPosition));
	float ditherValue = ((floord.x%ditherMods.x) + (floord.y%ditherMods.y) + (floord.x + floord.y)%ditherMods.z)%ditherMods.w;
	dither = clamp(ditherValue, 0, 1);
}

void DitherMode_float(float2 screenPosition, float mode, out float dither){
	int2 checker = frac(screenPosition) > 0.5;
	if (mode == 0)
	{
		if (screenPosition.y < 0) screenPosition.y = 1-screenPosition.y;
		checker = int2(checker.x, frac(screenPosition.y + sin(screenPosition.x*3.14159*12)*0.02) > 0.5);
	}
	else if (mode == 1)
	{
		float2 p = frac(screenPosition*2)-float2(0.5,0.5);
		checker = int2(checker.x, dot(p, p) > 0.25 ^ checker.y);
	}
	else if (mode == 2)
	{
		float2 p = frac(screenPosition*2);
		checker = int2(p.x < p.y+sin(screenPosition.x*3.14159*8)*0.05, 0);
	}
	dither = checker.x ^ checker.y ? 1 : 0;
}
#endif // SPRITE_SHEET_INCLUDED
