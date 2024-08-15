cmake_minimum_required(VERSION 3.10)

project(CalcManager VERSION 1.0)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Add source files
set(SOURCES
    CalculatorHistory.cpp
    CalculatorManager.cpp
    CEngine/calc.cpp
    CEngine/CalcUtils.cpp
    CEngine/History.cpp
    CEngine/CalcInput.cpp
    CEngine/Number.cpp
    CEngine/Rational.cpp
    CEngine/scicomm.cpp
    CEngine/scidisp.cpp
    CEngine/scifunc.cpp
    CEngine/RationalMath.cpp
    CEngine/scioper.cpp
    CEngine/sciset.cpp
    ExpressionCommand.cpp
    Ratpack/basex.cpp
    Ratpack/conv.cpp
    Ratpack/exp.cpp
    Ratpack/fact.cpp
    Ratpack/itrans.cpp
    Ratpack/itransh.cpp
    Ratpack/logic.cpp
    Ratpack/num.cpp
    Ratpack/rat.cpp
    Ratpack/support.cpp
    Ratpack/trans.cpp
    Ratpack/transh.cpp
    NumberFormattingUtils.cpp
    UnitConverter.cpp
)

# Add header files
set(HEADERS
    CalculatorHistory.h
    CalculatorManager.h
    CalculatorResource.h
    Command.h
    ExpressionCommand.h
    ExpressionCommandInterface.h
    "Header Files/CalcEngine.h"
    "Header Files/CalcUtils.h"
    "Header Files/CCommand.h"
    "Header Files/EngineStrings.h"
    "Header Files/History.h"
    "Header Files/ICalcDisplay.h"
    "Header Files/CalcInput.h"
    "Header Files/IHistoryDisplay.h"
    "Header Files/Number.h"
    "Header Files/RadixType.h"
    "Header Files/Rational.h"
    "Header Files/RationalMath.h"
    Ratpack/CalcErr.h
    Ratpack/ratconst.h
    Ratpack/ratpak.h
    NumberFormattingUtils.h
    UnitConverter.h
)

# Create static library
add_library(CalcManager STATIC ${SOURCES} ${HEADERS})

# Include directories
target_include_directories(CalcManager PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
    ${CMAKE_CURRENT_SOURCE_DIR}/Header Files
    ${CMAKE_CURRENT_SOURCE_DIR}/Ratpack
)

# Compiler options
target_compile_options(CalcManager PRIVATE
    $<$<CXX_COMPILER_ID:MSVC>:/W4 /WX>
    $<$<NOT:$<CXX_COMPILER_ID:MSVC>>:-Wall -Wextra -Werror -Wno-unused-variable -Wno-switch -Wno-missing-field-initializers -Wno-sign-compare>
)

# Preprocessor definitions
target_compile_definitions(CalcManager PRIVATE
    $<$<CONFIG:Debug>:_DEBUG>
    $<$<CONFIG:Release>:NDEBUG>
)

# Platform-specific settings
if(WIN32)
    target_compile_definitions(CalcManager PRIVATE UNICODE _UNICODE)
endif()

# Precompiled header
if(MSVC)
    target_precompile_headers(CalcManager PRIVATE pch.h)
else()
    # For non-MSVC compilers, you might want to include pch.h in your source files
    # or remove this part if precompiled headers are not used
endif()
