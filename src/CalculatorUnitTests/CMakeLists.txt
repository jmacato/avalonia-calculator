cmake_minimum_required(VERSION 3.10)

project(CalculatorUnitTests VERSION 1.0)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Find Python interpreter
find_package(Python COMPONENTS Interpreter REQUIRED)

# Define resource files with absolute paths
set(RESOURCE_FILES
        # Order of resource loading is important because calculator engine
        # needs the values in order to override duplicates properly.
        "${CMAKE_CURRENT_SOURCE_DIR}/Test.resw"
        "${CMAKE_CURRENT_SOURCE_DIR}/../Calculator/Resources/en-US/Resources.resw"
        "${CMAKE_CURRENT_SOURCE_DIR}/../Calculator/Resources/en-US/CEngineStrings.resw"
)

# Define output directory and file for generated header
set(GENERATED_HEADER_DIR ${CMAKE_CURRENT_BINARY_DIR}/generated_headers)
set(GENERATED_HEADER ${GENERATED_HEADER_DIR}/CombinedResources.h)
file(MAKE_DIRECTORY ${GENERATED_HEADER_DIR})

# Generate combined header file from all .resw files
add_custom_command(
        OUTPUT ${GENERATED_HEADER}
        COMMAND ${Python_EXECUTABLE}
        ${CMAKE_CURRENT_SOURCE_DIR}/resw_to_header.py
        ${GENERATED_HEADER}
        ${RESOURCE_FILES}
        DEPENDS ${RESOURCE_FILES} ${CMAKE_CURRENT_SOURCE_DIR}/resw_to_header.py
        COMMENT "Generating combined header for all .resw files"
)

# Add executable
add_executable(${PROJECT_NAME}
        CalcEngineTests.cpp
        CalculatorManagerTest.cpp
        ResourceProvider.cpp
        ${GENERATED_HEADER}
)

# Ensure the combined header is generated before building the target
add_custom_target(generate_resource_header DEPENDS ${GENERATED_HEADER})
add_dependencies(${PROJECT_NAME} generate_resource_header)

# Include directory for the generated header
target_include_directories(${PROJECT_NAME} PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}
        ${GENERATED_HEADER_DIR}
)

# Link libraries
target_link_libraries(${PROJECT_NAME} gtest gtest_main CalcManager)