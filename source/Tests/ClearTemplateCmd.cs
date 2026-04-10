using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BIMPlugins.ExtStorage;
using BIMPlugins.ExtStorage.Extensions;
using BIMPlugins.ExtStorage.Methods;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace BIMPlugins.Tests
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ClearTemplateCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = RevitAPI.Document;

            var parameterBinds = new List<ParameterBind>();

            var iter = doc.ParameterBindings.ForwardIterator();
            while (iter.MoveNext())
            {
                var paramBind = new ParameterBind(iter.Current as ElementBinding, iter.Key as InternalDefinition);
                if (!paramBind.CategorySet.IsEmpty)
                    parameterBinds.Add(paramBind);
            }

            var guidsDict = new Dictionary<string, Guid>();

            var paramDict = GetSortParametersDict(@"C:\Users\shibliev\Desktop\параметры_Прочее.txt");

            using (Transaction t = new Transaction(doc, "Очистить шаблон"))
            {
                t.Start();

                foreach (var param in doc.ToElements<SharedParameterElement>().Where(p => p.GetDefinition().ParameterGroup == BuiltInParameterGroup.INVALID).ToList())
                {
                    guidsDict.Add(param.Name, param.GuidValue);

                    try
                    {
                        doc.Delete(param.Id);
                    }
                    catch { }
                }

                foreach (var paramKvp in paramDict)
                {
                    var paramGroup = FindParamGroup(paramKvp.Key);

                    foreach (var paramName in paramKvp.Value)
                    {
                        var map = doc.ParameterBindings;

                        var paramBind = parameterBinds.FirstOrDefault(p => p.Name == paramName);
                        if (paramBind == null)
                            continue;

                        ElementBinding bind = paramBind.IsInstance ? new InstanceBinding(paramBind.CategorySet) : new TypeBinding(paramBind.CategorySet);

                        if (guidsDict.ContainsKey(paramName))
                        {
                            var extDef = ParameterMethods.FindExternalDefinition(paramName, guidsDict[paramName]);
                            map.Insert(extDef, bind, paramGroup);
                        }
                        else
                        {
                            map.Remove(paramBind.Definition);

                            map.ReInsert(paramBind.Definition, bind, paramGroup);
                        }

                        doc.ParameterBindings.ToDefinition(paramName).SetAllowVaryBetweenGroups(doc, paramBind.VariesAcrossGroups);
                    }
                }

                t.Commit();
            }

            return Result.Succeeded;
        }

        private  class ParameterBind(ElementBinding binding, InternalDefinition definition)
        {
            public string Name { get; } = definition.Name;
            public InternalDefinition Definition { get; } = definition;
            public bool IsInstance { get; } = binding is InstanceBinding;
            public CategorySet CategorySet { get; } = binding.Categories;
            public bool VariesAcrossGroups { get; } = definition.VariesAcrossGroups; 
        }

        private Dictionary<string, List<string>> GetSortParametersDict(string filePath)
        {
            var result = new Dictionary<string, List<string>>();
            string currentKey = null;

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                string rawLine = line;

                if (string.IsNullOrWhiteSpace(rawLine))
                    continue;

                if (rawLine.StartsWith("\t"))
                {
                    currentKey = rawLine.TrimStart('\t').Trim();

                    if (!result.ContainsKey(currentKey))
                        result[currentKey] = [];
                }
                else
                {
                    if (currentKey != null)
                        result[currentKey].Add(rawLine.Trim());
                }
            }

            return result;
        }

        private BuiltInParameterGroup FindParamGroup(string name)
        {
            return Enum.GetValues(typeof(BuiltInParameterGroup))
                .Cast<BuiltInParameterGroup>()
                .Where(g => g != BuiltInParameterGroup.INVALID)
                .FirstOrDefault(g => LabelUtils.GetLabelFor(g) == name);
        }
    }
}