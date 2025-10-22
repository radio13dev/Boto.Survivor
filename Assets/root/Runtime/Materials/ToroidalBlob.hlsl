#ifndef TOROIDALBLOB_INCLUDED
#define TOROIDALBLOB_INCLUDED

// IF YOU MAKE ANY CHANGES HERE DO THIS vvvvv
#define BLOB_COUNT 3	 // ALSO UPDATE IN: ToroidalBlobInit.cs
#define METABALL_COUNT 40 // ALSO UPDATE IN: ToroidalBlobInit.cs
// IF YOU MAKE ANY CHANGES HERE DO THIS ^^^^^


float4 _blob_acolor[BLOB_COUNT];
float4 _blob_bcolor[BLOB_COUNT];
float4 _blob_border[BLOB_COUNT];

float3 _metaball_position[METABALL_COUNT];
float _metaball_radiussqr[METABALL_COUNT];
int _metaball_index[METABALL_COUNT];

float sRGBtoLinear(float c)
{
	if (c <= 0.04045)
	{
		return c / 12.92;
	}
	else
	{
		return pow((c + 0.055) / 1.055, 2.4);
	}
}

float3 sRGBtoLinear(float3 srgbColor)
{
	return float3(sRGBtoLinear(srgbColor.r), sRGBtoLinear(srgbColor.g), sRGBtoLinear(srgbColor.b));
}

float LinearTosRGB(float c)
{
	if (c <= 0.0031308)
	{
		return c * 12.92;
	}
	else
	{
		return 1.055 * pow(c, 1.0 / 2.4) - 0.055;
	}
}

float3 LinearTosRGB(float3 linearColor)
{
	return float3(LinearTosRGB(linearColor.r), LinearTosRGB(linearColor.g), LinearTosRGB(linearColor.b));
}

float3 RGBtoHSL(float3 rgb)
{
	float3 hsl;

	float M = max(rgb.r, max(rgb.g, rgb.b)); // Maximum component
	float m = min(rgb.r, min(rgb.g, rgb.b)); // Minimum component
	float C = M - m; // Chroma

	hsl.z = (M + m) * 0.5; // Lightness

	if (C == 0) // Achromatic (grayscale)
	{
		hsl.x = 0; // Hue is undefined, set to 0
		hsl.y = 0; // Saturation is 0
	}
	else
	{
		// Saturation calculation
		hsl.y = (hsl.z > 0.5) ? C / (2 - M - m) : C / (M + m);

		// Hue calculation
		if (M == rgb.r)
		{
			hsl.x = (rgb.g - rgb.b) / C;
			if (rgb.g < rgb.b) hsl.x += 6;
		}
		else if (M == rgb.g)
		{
			hsl.x = (rgb.b - rgb.r) / C + 2;
		}
		else // M == rgb.b
		{
			hsl.x = (rgb.r - rgb.g) / C + 4;
		}
		hsl.x /= 6; // Normalize hue to [0, 1]
	}
	return hsl;
}

// Helper function for hue2rgb
float hue2rgb_component(float p_val, float q_val, float t_val)
{
	if (t_val < 0) t_val += 1;
	if (t_val > 1) t_val -= 1;
	if (t_val < 1.0/6.0) return p_val + (q_val - p_val) * 6 * t_val;
	if (t_val < 1.0/2.0) return q_val;
	if (t_val < 2.0/3.0) return p_val + (q_val - p_val) * (2.0/3.0 - t_val) * 6;
	return p_val;
}
float3 HSLtoRGB(float3 hsl)
{
	float3 rgb;

	if (hsl.y == 0) // Achromatic (grayscale)
	{
		rgb = hsl.z; // R, G, B are all equal to lightness
	}
	else
	{
		float q = (hsl.z < 0.5) ? hsl.z * (1 + hsl.y) : hsl.z + hsl.y - (hsl.z * hsl.y);
		float p = 2 * hsl.z - q;

		rgb.r = hue2rgb_component(p, q, hsl.x + 1.0/3.0);
		rgb.g = hue2rgb_component(p, q, hsl.x);
		rgb.b = hue2rgb_component(p, q, hsl.x - 1.0/3.0);
	}
	return rgb;
}

float4 lerp_rgb_using_HSL(float4 a, float4 b, float t)
{
	a.xyz = RGBtoHSL(LinearTosRGB(a.xyz));
	b.xyz = RGBtoHSL(LinearTosRGB(b.xyz));
	a = lerp(a,b,t);
	a.xyz = sRGBtoLinear(HSLtoRGB(a.xyz));
	return a;
}

void ToroidalBlob_float(float3 pos, float4 aIn, float4 bIn, float tintScale, out float4 aOut, out float4 bOut, out float infl){
	float3 checker = pos;
	
	float threshold = 0.7;
	float threshold2 = 0.73;
	
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
	float edge = 10;
	for (int i = 0; i < BLOB_COUNT; i++)
	{
		float blobInfluence = blobsInfluences[i];
		if (blobInfluence > infl)
		{
			best = i;
			
			if (infl > threshold)
			{
				edge = (blobInfluence - infl);
			}
			infl = blobInfluence;
		}
		else if (blobInfluence > threshold)
		{
			edge = min(edge, (infl - blobInfluence));
		}
	}
	 
	if(infl > threshold)
	{
		// Edge
		if (infl < threshold2 || (edge < threshold2 - threshold))
		{
			if (tintScale != 1)
			{
				aOut = lerp(aIn, _blob_border[best], tintScale); 
				bOut = lerp(bIn, _blob_border[best], tintScale); 
			}
			else
			{
				aOut = _blob_border[best];
				bOut = aOut;
			}
		}
		// Fill
		else
		{
			if (tintScale != 1)
			{
				aOut = lerp(aIn, _blob_acolor[best], tintScale); 
				bOut = lerp(bIn, _blob_bcolor[best], tintScale);//bIn + _blob_bcolor[best]*tintScale;
			}
			else
			{
				aOut = _blob_acolor[best];
				bOut = _blob_bcolor[best];
			}
		}
	}
	else
	{
		aOut = aIn; bOut = bIn;
	}
}


void ToroidalBlobHue_float(float3 pos, float4 aIn, float4 bIn, float tintScale, out float4 aOut, out float4 bOut, out float infl, out float hitIndex){
	
	float3 checker = pos;
	
	float threshold = 0.7;
	float threshold2 = 0.73;
	
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
	float edge = 10;
	for (int i = 0; i < BLOB_COUNT; i++)
	{
		float blobInfluence = blobsInfluences[i];
		if (blobInfluence > infl)
		{
			best = i;
			
			if (infl > threshold)
			{
				edge = (blobInfluence - infl);
			}
			infl = blobInfluence;
		}
		else if (blobInfluence > threshold)
		{
			edge = min(edge, (infl - blobInfluence));
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
			if (infl < threshold2 || (edge < threshold2 - threshold))
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
