#ifndef DITHER_INCLUDED
#define DITHER_INCLUDED

void Dither_float(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, out float dither){
	float2 floord = abs(floor(screenPosition));
	float ditherValue = (floord.x + floord.y)%2;
	dither = ditherValue;
}
void DitherCustom_float(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, float4 ditherMods, out float dither){
	float2 floord = abs(floor(screenPosition));
	float ditherValue = ((floord.x%ditherMods.x) + (floord.y%ditherMods.y) + (floord.x + floord.y)%ditherMods.z)%ditherMods.w;
	dither = clamp(ditherValue, 0, 1);
}
#endif // SPRITE_SHEET_INCLUDED
