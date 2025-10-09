#ifndef STRETCH_INCLUDED
#define STRETCH_INCLUDED

uniform float4 stretchBuffer[511];
void SelectStretchFromBuffer_float(float instanceId, out float4 stretch){
	stretch = stretchBuffer[(int)instanceId];
}
#endif // SPRITE_SHEET_INCLUDED
