#ifndef SPRITE_SHEET_INCLUDED
#define SPRITE_SHEET_INCLUDED

uniform float spriteAnimFrameBuffer[1023];
void SelectFrameFromBuffer_float(float instanceId, out float frame){
	frame = spriteAnimFrameBuffer[(int)instanceId];
}
#endif // SPRITE_SHEET_INCLUDED
