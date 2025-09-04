#ifndef DELAYED_BLAST_INCLUDED
#define DELAYED_BLAST_INCLUDED

uniform float torusMinBuffer[511];
void SelectTorusMinFromBuffer_float(float instanceId, out float torusMin){
	torusMin = torusMinBuffer[(int)instanceId];
}
#endif // SPRITE_SHEET_INCLUDED
