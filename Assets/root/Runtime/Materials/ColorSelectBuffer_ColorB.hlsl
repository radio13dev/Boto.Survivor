#ifndef COLORSELECTBUFFER_COLORB_INCLUDED
#define COLORSELECTBUFFER_COLORB_INCLUDED

uniform float4 colorBBuffer[511];
void SelectColorBBuffer_float(float instanceId, out float4 colorB){
	colorB = colorBBuffer[(int)instanceId];
}
#endif // COLORSELECTBUFFER_COLORB_INCLUDED