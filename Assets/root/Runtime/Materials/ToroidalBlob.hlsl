#ifndef TOROIDALBLOB_INCLUDED
#define TOROIDALBLOB_INCLUDED

// IF YOU MAKE ANY CHANGES HERE DO THIS vvvvv
#define BLOB_COUNT 6	 // ALSO UPDATE IN: ToroidalBlobInit.cs
#define METABALL_COUNT 40 // ALSO UPDATE IN: ToroidalBlobInit.cs
#define PI 3.141592653589793238462643383279502884
// IF YOU MAKE ANY CHANGES HERE DO THIS ^^^^^


float4 _blob_acolor[BLOB_COUNT];
float4 _blob_bcolor[BLOB_COUNT];
float4 _blob_border[BLOB_COUNT];

float3 _metaball_position[METABALL_COUNT];
float _metaball_radiussqr[METABALL_COUNT];
int _metaball_index[METABALL_COUNT];

void ToroidalBlobHue_float(float3 pos, float4 aIn, float4 bIn, float tintScale, out float4 aOut, out float4 bOut, out float infl, out float hitIndex){
	
	float3 checker = pos;
	
	float threshold = 0.0;
	
	float blobsInfluences[BLOB_COUNT];
	for (int i = 0; i < BLOB_COUNT; i++) blobsInfluences[i] = 0;
	
	for(int i = 0; i < METABALL_COUNT; i++)
	{
		int mbIndex = _metaball_index[i];
		if (mbIndex >= BLOB_COUNT) continue;
		
		float3 mbPos = _metaball_position[i];
		float mbRadSqr = _metaball_radiussqr[i];
		
		float currInfl = mbRadSqr;
		currInfl /= (pow(checker.x-mbPos.x,2.0) + pow(checker.y-mbPos.y,2.0) + pow(checker.z-mbPos.z,2.0));
		blobsInfluences[mbIndex] += currInfl;
	}

	// Find the thing with the highest influence
	int best = -1;
	infl = 0;
	float edge = 0;
	for (int i = 0; i < BLOB_COUNT; i++)
	{
		float blobInfluence = blobsInfluences[i];
		if (blobInfluence > infl)
		{
			best = i;
			
			if (infl > threshold)
			{
				edge = infl;
			}
			infl = blobInfluence;
		}
		else if (blobInfluence > threshold)
		{
			edge = max(edge, blobInfluence);
		}
	}
	 
	if(infl > threshold)
	{
		hitIndex = best;
	
		if (tintScale != 1)
		{
			aOut = aIn; bOut = bIn;
		}
		else
		{
			// Edge
			const float threshold2 = 0.02;
			if ((infl-edge) < threshold2)
			{
				aOut = aIn + _blob_border[hitIndex]; 
				bOut = aOut;
			}
			// Fill
			else
			{
				aOut = aIn + _blob_acolor[hitIndex]; 
				bOut = bIn + _blob_bcolor[hitIndex];
			}
		}
	}
	else
	{
		hitIndex = -1;
		aOut = aIn; bOut = bIn;
	}
}
#endif // TOROIDALBLOB_INCLUDED
