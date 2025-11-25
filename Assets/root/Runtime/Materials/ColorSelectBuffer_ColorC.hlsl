#ifndef COLORSELECTBUFFER_COLORC_INCLUDED
#define COLORSELECTBUFFER_COLORC_INCLUDED

uniform float4 colorCBuffer[511];
void SelectColorCBuffer_float(float instanceId, out float4 colorC){
	colorC = colorCBuffer[(int)instanceId];
}
#endif // COLORSELECTBUFFER_COLORC_INCLUDED
