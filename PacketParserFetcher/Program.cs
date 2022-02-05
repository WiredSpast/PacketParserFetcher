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
                if (parser != null /*&& parser.Container.QName.Name.Equals("_-3O")*/)
                {
                    var messageEventName = FindMessageEventName(abcFile, parserRequester.Container.QName.Name);
                    Console.WriteLine(messageEventName + " - " + parserRequester.Container.QName.Name + " - " + parser.Container.QName.Name);
                    var stack = new List<string>();
                    var indent = "  ";
                    var closingIn = new List<long>();
                    var code = new ASCode(abcFile, parser.Body);
                    var codeLines = new List<string>();
                    for (var i = 0; i < code.Count; i++)
                    {
                        var instruction = code[i];
                        closingIn = closingIn.Select(j => j - instruction.ToArray().Length).ToList();
                        switch (instruction.OP)
                        {
                            case OPCode.GetLocal:
                                stack.Add($"loc{((GetLocalIns) instruction).Register}");
                                break;
                            case OPCode.GetLocal_0:
                                stack.Add("loc0");
                                break;
                            case OPCode.GetLocal_1:
                                stack.Add("loc1");
                                break;
                            case OPCode.GetLocal_2:
                                stack.Add("loc2");
                                break;
                            case OPCode.GetLocal_3:
                                stack.Add("loc3");
                                break;
                            case OPCode.SetLocal:
                                codeLines.Add(
                                    $"{indent}loc{((SetLocalIns) instruction).Register} = {stack.Last()};");
                                stack.RemoveAt(stack.Count - 1);
                                break;
                            case OPCode.SetLocal_2:
                                codeLines.Add($"{indent}loc2 = {stack.Last()};");
                                stack.RemoveAt(stack.Count - 1);
                                break;
                            case OPCode.SetLocal_3:
                                codeLines.Add($"{indent}loc3 = {stack.Last()};");
                                stack.RemoveAt(stack.Count - 1);
                                break;
                            case OPCode.GetLex:
                                stack.Add(FindDeobfuscatedVariableName(abcFile, parser,
                                    ((GetLexIns) instruction).TypeName.Name));
                                break;
                            case OPCode.PushTrue:
                                stack.Add("true");
                                break;
                            case OPCode.PushFalse:
                                stack.Add("false");
                                break;
                            case OPCode.PushNull:
                                stack.Add("null");
                                break;
                            case OPCode.PushByte:
                                stack.Add($"{((PushByteIns) instruction).Value}");
                                break;
                            case OPCode.PushString:
                                stack.Add($"\"{((PushStringIns) instruction).Value}\"");
                                break;
                            case OPCode.PushNan:
                                stack.Add("NaN");
                                break;
                            case OPCode.NewArray:
                                var newArray = (NewArrayIns) instruction;
                                var arrayValues = string.Join(", ", stack.TakeLast(newArray.ArgCount));
                                stack.RemoveRange(stack.Count - newArray.ArgCount, newArray.ArgCount);
                                stack.Add($"[{arrayValues}]");
                                break;
                            case OPCode.NewObject:
                                var newObject = (NewObjectIns) instruction;
                                var components = string.Join(", ",
                                    stack.TakeLast(newObject.ArgCount * 2).Chunk(2).Select(e => $"{e[0]}: {e[1]}"));
                                stack.RemoveRange(stack.Count - (newObject.ArgCount * 2), newObject.ArgCount * 2);
                                stack.Add($"{{{components}}}");
                                break;
                            case OPCode.ReturnValue:
                                codeLines.Add($"{indent}return {stack.Last()};");
                                stack.RemoveAt(stack.Count - 1);
                                break;
                            case OPCode.IfNe:
                                codeLines.Add(
                                    $"{indent}if ({stack.ElementAt(stack.Count - 2)} == {stack.Last()}) {{");
                                stack.RemoveRange(stack.Count - 2, 2);
                                closingIn.Add(((IfNotEqualIns) instruction).Offset);
                                indent += "  ";
                                break;
                            case OPCode.IfNGt:
                                codeLines.Add(
                                    $"{indent}if ({stack.ElementAt(stack.Count - 2)} > {stack.Last()}) {{");
                                stack.RemoveRange(stack.Count - 2, 2);
                                closingIn.Add(((IfNotGreaterThanIns) instruction).Offset);
                                indent += "  ";
                                break;
                            case OPCode.IfLt:
                                var condition = $"{stack.ElementAt(stack.Count - 2)} < {stack.Last()}";
                                
                                for (var j = codeLines.Count - 1; j >= 0; j--)
                                {
                                    if (codeLines[j].Contains("%condition%"))
                                    {
                                        codeLines[j] = codeLines[j].Replace("%condition%", condition);
                                        break;
                                    }

                                    if (indent.Length > 2)
                                    {
                                        indent = indent.Remove(0, 2);
                                    }
                                }
                                codeLines.Add($"{indent}}}");
                                break;
                            case OPCode.IfFalse:
                                codeLines.Add($"{indent}if ({stack.Last()}) {{");
                                stack.RemoveAt(stack.Count - 1);
                                closingIn.Add(((IfFalseIns) instruction).Offset);
                                indent += "  ";
                                break;
                            case OPCode.Jump:
                                var jump = (JumpIns) instruction;
                                if (closingIn.Exists(i => i <= 0))
                                {
                                    indent = indent.Remove(0, 2);
                                    codeLines.Add($"{indent}}} else {{");
                                    indent += "  ";
                                    closingIn.Remove(0);
                                    closingIn.Add(jump.Offset);
                                }
                                else if(code.Count > i + 1 && code[i + 1].OP == OPCode.Label)
                                {
                                    codeLines.Add($"{indent}while(%condition%) {{");
                                    indent += "  ";
                                    i++;
                                }
                                break;
                            case OPCode.CallPropVoid:
                                var callPropVoid = (CallPropVoidIns) instruction;
                                var parametersVoid = string.Join(", ", stack.TakeLast(callPropVoid.ArgCount));
                                stack.RemoveRange(stack.Count - callPropVoid.ArgCount, callPropVoid.ArgCount);
                                codeLines.Add(
                                    $"{indent}{stack.Last()}.{callPropVoid.PropertyName.Name}({parametersVoid});");
                                stack.RemoveAt(stack.Count - 2);
                                break;
                            case OPCode.CallProperty:
                                var callProp = (CallPropertyIns) instruction;
                                var parameters = string.Join(", ", stack.TakeLast(callProp.ArgCount));
                                stack.RemoveRange(stack.Count - callProp.ArgCount, callProp.ArgCount);
                                stack.Add($"{stack.Last()}.{callProp.PropertyName.Name}({parameters})");
                                stack.RemoveAt(stack.Count - 2);
                                break;
                            case OPCode.FindProperty:
                                var findProp = (FindPropertyIns) instruction;
                                stack.Add(FindDeobfuscatedVariableName(abcFile, parser, findProp.PropertyName.Name));
                                break;
                            case OPCode.FindPropStrict:
                                var findPropStrict = (FindPropStrictIns) instruction;
                                stack.Add(FindDeobfuscatedVariableName(abcFile, parser,
                                    findPropStrict.PropertyName.Name));
                                break;
                            case OPCode.SetProperty:
                                var setProp = (SetPropertyIns) instruction;
                                if (stack.ElementAt(stack.Count - 2).StartsWith("loc") && !stack.ElementAt(stack.Count - 2).Equals("loc0"))
                                {
                                    codeLines.Add($"{indent}{stack.ElementAt(stack.Count - 2)}.{FindDeobfuscatedVariableName(abcFile, parser, setProp.PropertyName.Name)} = {stack.Last()};");
                                }
                                else
                                {
                                    codeLines.Add($"{indent}{FindDeobfuscatedVariableName(abcFile, parser, setProp.PropertyName.Name)} = {stack.Last()};");
                                }
                                stack.RemoveAt(stack.Count - 1);
                                break;
                            case OPCode.GetProperty:
                                var getProp = (GetPropertyIns) instruction;
                                stack.Add($"{stack.Last()}.{getProp.PropertyName.Name}");
                                stack.RemoveAt(stack.Count - 2);
                                break;
                            case OPCode.Swap:
                                stack.Add(stack.ElementAt(stack.Count - 2));
                                stack.RemoveAt(stack.Count - 3);
                                break;
                            case OPCode.Convert_i:
                                stack.Add($"parseInt({stack.Last()})");
                                stack.RemoveAt(stack.Count - 2);
                                break;
                            case OPCode.IncLocal_i:
                                var incLocal = (IncLocalIIns) instruction;
                                codeLines.Add($"{indent}loc{incLocal.Register}++;");
                                break;
                            case OPCode.CallSuper:
                                var callSuper = (CallSuperIns) instruction;
                                var parametersSuper = string.Join(", ", stack.TakeLast(callSuper.ArgCount));
                                stack.Add($"super.{callSuper.MethodName.Name}({parametersSuper})");
                                break;
                            case OPCode.ConstructProp:
                                var constrProp = (ConstructPropIns) instruction;
                                var constrPropParameters = string.Join(", ", stack.TakeLast(constrProp.ArgCount));
                                stack.Add($"new {constrProp.PropertyName.Name}({constrPropParameters})");
                                break;
                            case OPCode.Coerce:
                            case OPCode.PushScope:
                                break;
                            default:
                                codeLines.Add("" + instruction);
                                break;
                        }

                        foreach (var j in closingIn.FindAll(j => j <= 0))
                        {
                            indent = indent.Remove(0, 2);
                            codeLines.Add($"{indent}}}");
                        }

                        closingIn.RemoveAll(j => j <= 0);
                    }

                    foreach (var line in codeLines)
                    {
                        Console.WriteLine(line);
                    }
                }
            }
        }
    }
}

List<ASMethod> FindParserRequesters(ABCFile abcFile)
{
    return abcFile.Methods
        .FindAll(m => m.Name is "getParser");
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
        var code = new ASCode(abcFile, method.Body);
        if (code[2] is GetLexIns)
        {
            var getLex = (GetLexIns) code[2];
            if (getLex.TypeName.Name.Equals(obfuscatedName))
            {
                return method.Name;
            }
        } 
        else if (code[3] is GetPropertyIns)
        {
            var getLex = (GetPropertyIns) code[3];
            if (getLex.PropertyName.Name.Equals(obfuscatedName))
            {
                return method.Name;
            }
        }
    }

    return obfuscatedName;
}