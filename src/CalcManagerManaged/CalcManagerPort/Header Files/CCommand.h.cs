// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

/****************************Module*Header***********************************
* Module Name: CCommand.h
*
* Module Description:
*       Resource ID's for the Engine Commands exposed.
*
* Warnings:
*
* Created: 13-Feb-2008
*
\****************************************************************************/

// The following are the valid id's which can be passed to CCalcEngine::ProcessCommand

namespace CalcEngine;

public static class CCommand
{
    public const int IdmHex = 313;
    public const int IdmDec = 314;
    public const int IdmOct = 315;
    public const int IdmBin = 316;
    public const int IdmQword = 317;
    public const int IdmDword = 318;
    public const int IdmWord = 319;
    public const int IdmByte = 320;
    public const int IdmDeg = 321;
    public const int IdmRad = 322;
    public const int IdmGrad = 323;
    public const int IdmDegrees = 324;

    public const int IdcHex = IdmHex;
    public const int IdcDec = IdmDec;
    public const int IdcOct = IdmOct;
    public const int IdcBin = IdmBin;

    public const int IdcDeg = IdmDeg;
    public const int IdcRad = IdmRad;
    public const int IdcGrad = IdmGrad;
    public const int IdcDegrees = IdmDegrees;

    public const int IdcQword = IdmQword;
    public const int IdcDword = IdmDword;
    public const int IdcWord = IdmWord;
    public const int IdcByte = IdmByte;

    // Key IDs:
    // These id's must be consecutive from IDC_FIRSTCONTROL to IDC_LASTCONTROL.
    // The actual values don't matter but the order and sequence are very important.
    // Also, the order of the controls must match the order of the control names
    // in the string table.
    // For example you want to declare the color for the control IDC_ST_AVE
    // Find the string id for that control from the rc file
    // Now define the control's id as IDC_FRISTCONTROL+stringID(IDC_ST_AVE)
    public const int IdcFirstcontrol = IdcSign;
    public const int IdcSign = 80;
    public const int IdcClear = 81;
    public const int IdcCentr = 82;
    public const int IdcBack = 83;

    public const int IdcPnt = 84;

    // Hole  85

    public const int IdcAnd = 86; // Binary operators must be between IDC_AND and IDC_PWR
    public const int IdcOr = 87;
    public const int IdcXor = 88;
    public const int IdcLshf = 89;
    public const int IdcRshf = 90;
    public const int IdcDiv = 91;
    public const int IdcMul = 92;
    public const int IdcAdd = 93;
    public const int IdcSub = 94;
    public const int IdcMod = 95;
    public const int IdcRoot = 96;
    public const int IdcPwr = 97;

    public const int IdcUnaryfirst = IdcChop;
    public const int IdcChop = 98; // Unary operators must be between IDC_CHOP and IDC_EQU
    public const int IdcRol = 99;
    public const int IdcRor = 100;
    public const int IdcCom = 101;

    public const int IdcSin = 102;
    public const int IdcCos = 103;
    public const int IdcTan = 104;

    public const int IdcSinh = 105;
    public const int IdcCosh = 106;
    public const int IdcTanh = 107;

    public const int IdcLn = 108;
    public const int IdcLog = 109;
    public const int IdcSqrt = 110;
    public const int IdcSqr = 111;
    public const int IdcCub = 112;
    public const int IdcFac = 113;
    public const int IdcRec = 114;
    public const int IdcDms = 115;
    public const int IdcCuberoot = 116; // x ^ 1/3
    public const int IdcPow10 = 117; // 10 ^ x
    public const int IdcPercent = 118;
    public const int IdcUnarylast = IdcPercent;

    public const int IdcFe = 119;
    public const int IdcPi = 120;
    public const int IdcEqu = 121;

    public const int IdcMclear = 122;
    public const int IdcRecall = 123;
    public const int IdcStore = 124;
    public const int IdcMplus = 125;
    public const int IdcMminus = 126;

    public const int IdcExp = 127;

    public const int IdcOpenp = 128;
    public const int IdcClosep = 129;

    public const int Idc0 = 130; // The controls for 0 through F must be consecutive and in order
    public const int Idc1 = 131;
    public const int Idc2 = 132;
    public const int Idc3 = 133;
    public const int Idc4 = 134;
    public const int Idc5 = 135;
    public const int Idc6 = 136;
    public const int Idc7 = 137;
    public const int Idc8 = 138;
    public const int Idc9 = 139;
    public const int IdcA = 140;
    public const int IdcB = 141;
    public const int IdcC = 142;
    public const int IdcD = 143;
    public const int IdcE = 144;
    public const int IdcF = 145; // this is last control ID which must match the string table
    public const int IdcInv = 146;
    public const int IdcSetResult = 147;
    public const int IdcStringMappedValues = 400;
    public const int IdcUnaryextendedfirst = IdcStringMappedValues;

    public const int IdcSec = 400; // Secant

    // 401 reserved for inverse
    public const int IdcCsc = 402; // Cosecant

    // 403 reserved for inverse
    public const int IdcCot = 404; // Cotangent
                                   // 405 reserved for inverse

    public const int IdcSech = 406; // Hyperbolic Secant

    // 407 reserved for inverse
    public const int IdcCsch = 408; // Hyperbolic Cosecant

    // 409 reserved for inverse
    public const int IdcCoth = 410; // Hyperbolic Cotangent
                                    // 411 reserved for inverse

    public const int IdcPow2 = 412; // 2 ^ x
    public const int IdcAbs = 413; // Absolute Value
    public const int IdcFloor = 414; // Floor
    public const int IdcCeil = 415; // Ceiling

    public const int IdcRolc = 416; // Rotate Left Circular
    public const int IdcRorc = 417; // Rotate Right Circular

    public const int IdcUnaryextendedlast = IdcRorc;

    public const int IdcLastcontrol = IdcCeil;

    public const int IdcBinaryextendedfirst = 500;
    public const int IdcLogbasey = 500; // logy(x)
    public const int IdcNand = 501; // Nand
    public const int IdcNor = 502; // Nor

    public const int IdcRshfl = 505; // Right Shift Logical
    public const int IdcBinaryextendedlast = IdcRshfl;

    public const int IdcRand = 600; // Random
    public const int IdcEuler = 601; // e Constant

    public const int IdcBineditstart = 700;
    public const int IdcBinpos0 = 700;
    public const int IdcBinpos1 = 701;
    public const int IdcBinpos2 = 702;
    public const int IdcBinpos3 = 703;
    public const int IdcBinpos4 = 704;
    public const int IdcBinpos5 = 705;
    public const int IdcBinpos6 = 706;
    public const int IdcBinpos7 = 707;
    public const int IdcBinpos8 = 708;
    public const int IdcBinpos9 = 709;
    public const int IdcBinpos10 = 710;
    public const int IdcBinpos11 = 711;
    public const int IdcBinpos12 = 712;
    public const int IdcBinpos13 = 713;
    public const int IdcBinpos14 = 714;
    public const int IdcBinpos15 = 715;
    public const int IdcBinpos16 = 716;
    public const int IdcBinpos17 = 717;
    public const int IdcBinpos18 = 718;
    public const int IdcBinpos19 = 719;
    public const int IdcBinpos20 = 720;
    public const int IdcBinpos21 = 721;
    public const int IdcBinpos22 = 722;
    public const int IdcBinpos23 = 723;
    public const int IdcBinpos24 = 724;
    public const int IdcBinpos25 = 725;
    public const int IdcBinpos26 = 726;
    public const int IdcBinpos27 = 727;
    public const int IdcBinpos28 = 728;
    public const int IdcBinpos29 = 729;
    public const int IdcBinpos30 = 730;
    public const int IdcBinpos31 = 731;
    public const int IdcBinpos32 = 732;
    public const int IdcBinpos33 = 733;
    public const int IdcBinpos34 = 734;
    public const int IdcBinpos35 = 735;
    public const int IdcBinpos36 = 736;
    public const int IdcBinpos37 = 737;
    public const int IdcBinpos38 = 738;
    public const int IdcBinpos39 = 739;
    public const int IdcBinpos40 = 740;
    public const int IdcBinpos41 = 741;
    public const int IdcBinpos42 = 742;
    public const int IdcBinpos43 = 743;
    public const int IdcBinpos44 = 744;
    public const int IdcBinpos45 = 745;
    public const int IdcBinpos46 = 746;
    public const int IdcBinpos47 = 747;
    public const int IdcBinpos48 = 748;
    public const int IdcBinpos49 = 749;
    public const int IdcBinpos50 = 750;
    public const int IdcBinpos51 = 751;
    public const int IdcBinpos52 = 752;
    public const int IdcBinpos53 = 753;
    public const int IdcBinpos54 = 754;
    public const int IdcBinpos55 = 755;
    public const int IdcBinpos56 = 756;
    public const int IdcBinpos57 = 757;
    public const int IdcBinpos58 = 758;
    public const int IdcBinpos59 = 759;
    public const int IdcBinpos60 = 760;
    public const int IdcBinpos61 = 761;
    public const int IdcBinpos62 = 762;
    public const int IdcBinpos63 = 763;
    public const int IdcBineditend = 763;

    // The strings in the following range IDS_ENGINESTR_FIRST ... IDS_ENGINESTR_MAX are strings allocated in the
    // resource for the purpose internal to Engine and cant be used by the clients
    public const int IdsEnginestrFirst = 0;
    public const int IdsEnginestrMax = 200;
}
