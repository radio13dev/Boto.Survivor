#ifndef COLORSELECTBUFFER_COLORA_INCLUDED
#define COLORSELECTBUFFER_COLORA_INCLUDED

uniform float4 colorABuffer[511];
void SelectColorABuffer_float(float instanceId, out float4 colorA){
	colorA = colorABuffer[(int)instanceId];
}
#endif // COLORSELECTBUFFER_COLORA_INCLUDED
