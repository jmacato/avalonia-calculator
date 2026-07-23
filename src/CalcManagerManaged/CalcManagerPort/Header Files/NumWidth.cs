// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace CalcEngine;

// This is expected to be in same order as IDM_QWORD, IDM_DWORD etc.
public enum NumWidth
{
    QwordWidth, // Number width of 64 bits mode (default)
    DwordWidth, // Number width of 32 bits mode
    WordWidth, // Number width of 16 bits mode
    ByteWidth // Number width of 16 bits mode
};
