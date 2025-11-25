#ifndef LIFESPAN_INCLUDED
#define LIFESPAN_INCLUDED

uniform float lifespanBuffer[511];
void SelectLifespanFromBuffer_float(float instanceId, out float lifespan){
	lifespan = lifespanBuffer[(int)instanceId];
}
#endif // SPRITE_SHEET_INCLUDED
