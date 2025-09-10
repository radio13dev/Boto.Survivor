#ifndef DELAYED_BLAST_ANGLED_INCLUDED
#define DELAYED_BLAST_ANGLED_INCLUDED

uniform float torusAngleBuffer[511];
void SelectTorusAngleFromBuffer_float(float instanceId, out float torusAngle){
	torusAngle = torusAngleBuffer[(int)instanceId];
}
#endif // DELAYED_BLAST_ANGLED_INCLUDED
