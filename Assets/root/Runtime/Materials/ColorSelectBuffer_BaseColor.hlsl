#ifndef COLORSELECTBUFFER_BASECOLOR_INCLUDED
#define COLORSELECTBUFFER_BASECOLOR_INCLUDED

uniform float4 colorBaseColorBuffer[511];
void SelectColorBaseColorBuffer_float(float instanceId, out float4 baseColor){
	baseColor = colorBaseColorBuffer[(int)instanceId];
}
#endif // COLORSELECTBUFFER_BASECOLOR_INCLUDED
