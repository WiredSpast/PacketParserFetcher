using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;

namespace PacketParserFetcher;

public enum ParserComponent
{
    MethodSetup,
    PacketNotNull,
    Read,
    ReadAsInt,
    ReturnTrue,
    ReturnFalse,
    SetLocalInt,
    SetLocalNull,
    ReadLocal,
    ForLoop,
    ObjectCreation,
    Unknown,
    UnnecessaryGetLocal0
}

public static class ParserComponentIdentifier
{
    private static readonly Dictionary<ParserComponent, OPCode[][]> ComponentStructures = new();
    
    static ParserComponentIdentifier()
    {
        ComponentStructures[ParserComponent.MethodSetup] = new[]{
            new[] {OPCode.GetLocal_0, OPCode.PushScope}
        };
        ComponentStructures[ParserComponent.Read] = new[]
        {
            new[] {OPCode.GetLocal_1, OPCode.CallProperty, OPCode.FindProperty, OPCode.Swap, OPCode.SetProperty},
            new[] {OPCode.GetLocal_0, OPCode.GetLocal_1, OPCode.CallProperty, OPCode.SetProperty}
        };
        ComponentStructures[ParserComponent.ReadAsInt] = new[]
        {
            new[] {OPCode.GetLocal_1, OPCode.CallProperty, OPCode.Convert_i, OPCode.FindProperty, OPCode.Swap, OPCode.SetProperty},
        };
        ComponentStructures[ParserComponent.ReadLocal] = new[]
        {
            new[] {OPCode.GetLocal_1, OPCode.CallProperty, OPCode.Convert_i, OPCode.SetLocal_3}
        };
        ComponentStructures[ParserComponent.ReturnTrue] = new[]
        { 
            new[] {OPCode.PushTrue, OPCode.ReturnValue}
        };
        ComponentStructures[ParserComponent.ReturnFalse] = new[]
        {  
            new[] {OPCode.PushFalse, OPCode.ReturnValue}
        };
        ComponentStructures[ParserComponent.PacketNotNull] = new[]
        {
            new[] {OPCode.GetLocal_1, OPCode.PushNull, OPCode.IfNe, OPCode.PushFalse, OPCode.ReturnValue}
        };
        ComponentStructures[ParserComponent.SetLocalInt] = new[]
        {
            new[] {OPCode.PushByte,OPCode.SetLocal},
            new[] {OPCode.PushByte,OPCode.SetLocal_0},
            new[] {OPCode.PushByte,OPCode.SetLocal_1},
            new[] {OPCode.PushByte,OPCode.SetLocal_2},
            new[] {OPCode.PushByte,OPCode.SetLocal_3}
        };
        ComponentStructures[ParserComponent.SetLocalNull] = new[]
        {
            new[] {OPCode.PushNull,OPCode.SetLocal},
            new[] {OPCode.PushNull,OPCode.SetLocal_0},
            new[] {OPCode.PushNull,OPCode.SetLocal_1},
            new[] {OPCode.PushNull,OPCode.SetLocal_2},
            new[] {OPCode.PushNull,OPCode.SetLocal_3}
        };
        ComponentStructures[ParserComponent.UnnecessaryGetLocal0] = new[]
        {
            new[] {OPCode.GetLocal_0}
        };
    }
    
    public static ParserComponent Identify(ASCode code, int index)
    {
        foreach (var structure in ComponentStructures)
        {
            foreach (var subStructure in structure.Value)
            {
                if (code.Skip(index).Take(subStructure.Length).Select(c => c.OP).SequenceEqual(subStructure))
                {
                    return structure.Key;
                }
            }
        }

        return ParserComponent.Unknown;
    }

    public static String IdentifyLocal(ASInstruction code)
    {
        switch (code.OP)
        {
            case OPCode.SetLocal:
                return "loc" + ((SetLocalIns) code).Register;
            case OPCode.SetLocal_1:
                return "loc1";
            case OPCode.SetLocal_2:
                return "loc2";
            case OPCode.SetLocal_3:
                return "loc3";
        }

        return "unknownLocal";
    }
}