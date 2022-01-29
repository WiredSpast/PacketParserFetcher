using Flazzy;
using Flazzy.ABC;
using Flazzy.ABC.AVM2;
using Flazzy.ABC.AVM2.Instructions;
using Flazzy.Tags;

string path = @"../../../SwfClients/WIN63-202201171612-715314201/HabboAir.swf";
var flash = new ShockwaveFlash(path);

flash.Disassemble();
foreach (TagItem tag in flash.Tags)
{
    if (tag.Kind == TagKind.DoABC)
    {
        var doABCTag = (DoABCTag) tag;
        if (doABCTag.Name.Equals("merged"))
        {
            var abcFile = new ABCFile(doABCTag.ABCData);
            
            foreach (var parserRequester in FindParserRequesters(abcFile))
            {
                var parser = FindParser(abcFile, parserRequester.ReturnType.Name);
                if (parser != null)
                {
                    Console.WriteLine(FindMessageEventName(abcFile, parserRequester.Container.QName.Name) + " - " + parserRequester.Container.QName.Name + " - " + parser.Container.QName.Name);
                    try
                    {
                        var code = new ASCode(abcFile, parser.Body);
                        for (var i = 0; i < code.Count; i++)
                        {
                            if (code[i].OP == OPCode.GetLocal_1 &&
                                i + 4 < code.Count &&
                                code[i + 1].OP == OPCode.CallProperty &&
                                code[i + 2].OP == OPCode.FindProperty &&
                                code[i + 3].OP == OPCode.Swap &&
                                code[i + 4].OP == OPCode.SetProperty)
                            {
                                var callProp = (CallPropertyIns) code[i + 1];
                                var findProp = (FindPropertyIns) code[i + 2];
                                var deobfuscatedName =
                                    FindDeobfuscatedVariableName(abcFile, parser, findProp.PropertyName.Name);
                                Console.WriteLine("\t" + deobfuscatedName + " = packet." + callProp.PropertyName.Name + "();");
                                i += 4;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("\tError");
                    }
                    Console.WriteLine();
                }
            }
        }
    }
}

List<ASMethod> FindParserRequesters(ABCFile abcFile)
{
    return abcFile.Methods
        .FindAll(m => m.Name != null)
        .FindAll(m => m.Name.Equals("getParser"));
}

string FindMessageEventName(ABCFile abcFile, string className)
{
    foreach (var method in abcFile.Methods.FindAll(m => m.IsConstructor && m.Name != null && m.Name.Contains("MessageEvent")))
    {
        if (method.Container.QName.Name.Equals(className))
        {
            return method.Name;
        }
    }
    return className;
}

ASMethod? FindParser(ABCFile abcFile, string className)
{
    return abcFile.Methods
        .Find(m => m.Name is "parse" 
                   && m.Container.QName.Name.Equals(className));
}

string FindDeobfuscatedVariableName(ABCFile abcFile, ASMethod parser, string obfuscatedName)
{
    foreach (var method in parser.Container.GetMethods())
    {
        var getLex = (GetLexIns) new ASCode(abcFile, method.Body)[2];
        if (getLex.TypeName.Name.Equals(obfuscatedName))
        {
            return method.Name;
        }
    }

    return obfuscatedName;
}