#pragma once

#include "CalculatorResource.h"
#include <string>
#include <unordered_map>

namespace CalculationManager
{
    class ResourceProvider : public IResourceProvider
    {
    public:
        ResourceProvider();
        std::wstring GetCEngineString(std::wstring_view id) override;

    private:
        std::unordered_map<std::wstring, std::wstring> m_resourceMap;
    };
}
