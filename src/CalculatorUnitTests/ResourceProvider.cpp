#include "ResourceProvider.h"
#include "CombinedResources.h"
#include <stdexcept>
#include <algorithm>

namespace CalculationManager
{
    ResourceProvider::ResourceProvider()
    {

        // Ensure required strings are present
        const std::wstring requiredStrings[] = {L"sDecimal", L"sThousand", L"sGrouping"};
        for (const auto& str : requiredStrings)
        {
            if (Resources::StringResources.find(str) == Resources::StringResources.end())
            {
                throw std::runtime_error("Required resource string not found: " + std::string(str.begin(), str.end()));
            }
        }
    }

    std::wstring ResourceProvider::GetCEngineString(std::wstring_view id)
    {
        std::wstring wid(id);
        auto it = Resources::StringResources.find(wid);
        if (it != Resources::StringResources.end())
        {
            return it->second;
        }
        else
        {
            // If the string is not found, return an empty string or throw an exception
            // depending on your error handling strategy
            return L"";
            // throw std::runtime_error("Resource string not found: " + std::string(id.begin(), id.end()));
        }
    }
}