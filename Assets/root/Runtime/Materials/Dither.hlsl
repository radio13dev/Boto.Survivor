#ifndef DITHER_INCLUDED
#define DITHER_INCLUDED

void Dither_float(float2 screenPosition, float3 objectWorldPosition, float3 cameraPosition, out float dither){
	float2 floord = abs(floor(screenPosition));
	float ditherValue = (floord.x + floord.y)%2;
	dither = ditherValue;
}
#endif // SPRITE_SHEET_INCLUDED
