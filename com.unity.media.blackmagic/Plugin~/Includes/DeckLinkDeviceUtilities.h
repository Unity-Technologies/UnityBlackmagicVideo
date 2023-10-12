#pragma once

#include "../Common.h"

namespace MediaBlackmagic
{
    struct PixelFormatMapping
    {
        unsigned int id;
        const char* name;
    };

    struct ChromaticityCoordinates
    {
        double RedX;
        double RedY;
        double GreenX;
        double GreenY;
        double BlueX;
        double BlueY;
        double WhiteX;
        double WhiteY;
    };

    enum class EOTF { SDR = 0, HDR = 1, PQ = 2, HLG = 3 };

    // Define conventional display primaries and reference white for colorspace.
    const ChromaticityCoordinates kDefaultRec2020Colorimetrics = { 0.708, 0.292, 0.170, 0.797, 0.131, 0.046, 0.3127, 0.3290 };
    const double kDefaultMaxDisplayMasteringLuminance = 1000.0;
    const double kDefaultMinDisplayMasteringLuminance = 0.0001;
    const double kDefaultMaxCLL = 1000.0;
    const double kDefaultMaxFALL = 50.0;

    struct HDRMetadata
    {
        HDRMetadata() :
            EOTF(static_cast<uint32_t>(EOTF::HLG)),
            referencePrimaries(kDefaultRec2020Colorimetrics),
            maxDisplayMasteringLuminance(kDefaultMaxDisplayMasteringLuminance),
            minDisplayMasteringLuminance(kDefaultMinDisplayMasteringLuminance),
            maxCLL(kDefaultMaxCLL),
            maxFALL(kDefaultMaxFALL) {}

        uint32_t EOTF;
        ChromaticityCoordinates referencePrimaries;
        double maxDisplayMasteringLuminance;
        double minDisplayMasteringLuminance;
        double maxCLL;
        double maxFALL;
    };

    static PixelFormatMapping kPixelFormatMappings[] =
    {
        { bmdFormat8BitYUV,		"8-bit YUV" },
        { bmdFormat10BitYUV,	"10-bit YUV" },
        { bmdFormat8BitARGB,	"8-bit ARGB" },
        { bmdFormat8BitBGRA,	"8-bit BGRA" },
        { bmdFormat10BitRGB,	"10-bit RGB" },
        { bmdFormat12BitRGB,	"12-bit RGB" },
        { bmdFormat12BitRGBLE,	"12-bit RGBLE" },
        { bmdFormat10BitRGBXLE,	"10-bit RGBXLE" },
        { bmdFormat10BitRGBX,	"10-bit RGBX" },
        { bmdFormatH265,		"H.265" },
        { 0, NULL }
    };

    static inline const char* getPixelFormatName(PixelFormatMapping* mappings, unsigned int id)
    {
        while (mappings->name != NULL)
        {
            if (mappings->id == id)
            {
                return mappings->name;
            }
            ++mappings;
        }

        return "Unknown";
    }

    static inline std::string GetDeckLinkStatus(IDeckLinkStatus* deckLinkStatus,
                                                BMDDeckLinkStatusID statusId,
                                                std::string& value)
    {
        HRESULT result;
        dllonglong intVal;
        std::string message = "";

        switch (statusId)
        {
        case bmdDeckLinkStatusLastVideoOutputPixelFormat:
        case bmdDeckLinkStatusCurrentVideoInputPixelFormat:
            result = deckLinkStatus->GetInt(statusId, &intVal);
            break;
        default:
            message = "Unknown status ID:" + std::to_string(statusId);
            return message;
        }

        if (FAILED(result))
        {
            /*
             * Failed to retrieve the status value. Don't complain as this is
             * expected for different hardware configurations.
             *
             * e.g.
             * A device that doesn't support automatic mode detection will fail
             * a request for bmdDeckLinkStatusDetectedVideoInputMode.
             */
            message = "Failed to retrieve the status value.";
            return message;
        }

        switch (statusId)
        {
        case bmdDeckLinkStatusCurrentVideoInputPixelFormat:
        case bmdDeckLinkStatusLastVideoOutputPixelFormat:
            value = getPixelFormatName(kPixelFormatMappings, (BMDPixelFormat)intVal);
            break;
        default:
            break;
        }

        return message;
    }
}
