Shader "Hidden/KeyingAndCompositing"
{
	Properties
	{
	}

	HLSLINCLUDE

#pragma vertex Vert

#pragma target 4.5
#pragma only_renderers d3d11 ps4 xboxone vulkan metal switch

#include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

	TEXTURE2D_X(_CustomBuffer);
	TEXTURE2D_X(_OverLayer);
	Texture2D _Background;
    float scaleMatte;
	float scaleMaxRB;
	float3 preScaleRGB;
	float3 preOffsetRGB;
    float display;
	float despill_amount;
	float levelMin;
	float levelMax;
	float2 cropTopLeft;
	float2 cropBottomRight;
	float doKeying;


    float3 despill_green(float3 color, float amount)
    {
        float3 result = color;
        float3 suppressed = color;
        suppressed.g = min((color.r+color.b)*0.5, suppressed.g);
        result = lerp( color, suppressed, amount );
        return result;
    }
    float InvLerp( float a, float b, float v )
    {
        return ( v - a ) / ( b - a);
    }
	float Crop( float2 uv, float2 ctl, float2 cbr, float a)
    {
        float resA = 0.0f;
        if ( uv.x > ctl.x && uv.x < cbr.x &&
            uv.y > cbr.y )
        {
           resA = a;
        }

        if (uv.y > ctl.y)
        {
            resA = 0.0f;
        }
        return resA;
    }
	float4 Compositing(Varyings varyings) : SV_Target
	{
		float depth = LoadCameraDepth(varyings.positionCS.xy);
		PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
	    float2 uv = posInput.positionNDC.xy;
		float4 col = _Background.Sample(s_point_clamp_sampler, uv);
		float4 overPixel = SAMPLE_TEXTURE2D_X_LOD(_OverLayer, s_point_clamp_sampler, uv, 0);
	    float4 Out = col;
        float4 colk = float4( col.r  * preScaleRGB.r, col.g * preScaleRGB.g, col.b * preScaleRGB.b, col.a);
        colk = float4( colk.r + preOffsetRGB.r, colk.g + preOffsetRGB.g, colk.b + preOffsetRGB.b, colk.a); // col offset and scaled pre-key

        float g_minus_max_r_b = 0;
        g_minus_max_r_b = clamp(colk.g - (scaleMaxRB)*max(colk.r,colk.b), 0.0f, 1.0f);
        float a = clamp((scaleMatte)*g_minus_max_r_b,0.0f, 1.0f);
        a = 1.0 - a;  // invert the matte
        a = clamp( InvLerp( levelMin, levelMax, a), 0, 1); // scale the matte post
        float3 result_color = float3(col.rgb);

        if (despill_amount > 0)
        {
            result_color = despill_green(float3(col.rgb), despill_amount);
        }

        if ( display == 0.0f) // Result
        {
            a = Crop( uv, cropTopLeft, cropBottomRight, a);
            Out = float4( result_color, a);
        }
        else if (display == 1.0f) // Front no despill
        {
            Out = float4( col.xyz, 1);
        }
        else if (display == 2.0f)  //  Matte
        {
            a = Crop( uv, cropTopLeft, cropBottomRight, a);
            Out = float4( a, a, a, 1);
        }
        else if (display == 3.0f) // Front despill
        {
            Out = float4( result_color.xyz, 1);
        }
        else if (display == 4.0f) // Result no despill
        {
            a = Crop( uv, cropTopLeft, cropBottomRight, a);
            Out = float4( col.xyz, a);
        }
        else if (display == 5.0f) // Raw matte
        {
            Out = float4( g_minus_max_r_b, g_minus_max_r_b, g_minus_max_r_b,1);
        }
        else if (display == 6.0f)  // Invert Raw matte
        {
            a = 1.0 - g_minus_max_r_b;
            Out = float4( a,a,a, 1);
        }
        else if (display == 7.0f) // Front pre-key
        {
            Out = float4( colk.r, colk.g, colk.b, 1);
        }
        else // Default to result
        {
            a = Crop( uv, cropTopLeft, cropBottomRight, a);
            Out = float4( result_color, a);
        }

	    if (doKeying > 0.0f)
	    {
    	    // do the compositing
	        Out = (a) * Out.rgba + (1.f - a) * overPixel;
            Out.a  = a;
	    }

	    return Out;
    }

	// We need this copy because we can't sample and write to the same render target (Camera color buffer)
	float4 Copy(Varyings varyings) : SV_Target
	{
		float depth = LoadCameraDepth(varyings.positionCS.xy);
		PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

		return float4(LOAD_TEXTURE2D_X_LOD(_CustomBuffer, posInput.positionSS.xy, 0).rgb, 1);
	}

	ENDHLSL

	SubShader
	{
		Pass
		{
			Name "Compositing"

			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma fragment Compositing
			ENDHLSL
		}

		Pass
		{
			Name "Copy"

			ZWrite Off
			ZTest Always
			Blend Off
			Cull Off

			HLSLPROGRAM
				#pragma fragment Copy
			ENDHLSL
		}
	}
	Fallback Off
}
