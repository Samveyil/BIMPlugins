using Autodesk.Revit.DB;
using BIMPlugins.ExtStorage.Extensions;
using System;
using System.Linq;

namespace BIMPlugins.Test2dRebar.Classes
{
    public static class RebarMethods
    {
        public static Guid IdGuid { get; } = new Guid("7289385b-86de-4ac5-bd2a-3e5f004b542d");                 // OLP_Id
        public static Guid TypeGuid { get; } = new Guid("215d6c56-3700-4db9-a5f5-53ec85b36daa");               // OLP_Зона расположения
        public static Guid StepGuid { get; } = new Guid("5d7cb726-ac59-4f05-a902-8fdffa796d15");               // ADSK_Шаг элементов
        public static Guid UseScheduleGuid { get; } = new Guid("b220b6e8-254f-479f-95b8-62fc7123b098");        // OLP_Учет в спецификации
        public static Guid DimenAGuid { get; } = new Guid("b10d2260-5080-470d-be69-e136df3b45f6");             // OLP_Арм_Аdef
        public static Guid FormGuid { get; } = new Guid("9fd2ad8f-69f7-4d6e-9261-8d50de85ac9d");               // OLP_Арм_Форма
        public static Guid PrefixGuid { get; } = new Guid("dce379c0-5e32-4695-b16a-d76ef0100172");             // OLP_Арм_Позиция_Префикс

        public static Parameter GetSymbolParameter(this FamilyInstance rebar, Guid paramGuid)
        {
            var sourceFormParam = rebar.Symbol.get_Parameter(paramGuid);
            sourceFormParam ??= rebar.GetSubComponentIds()
                .FirstOrDefault()?
                .ToElement<FamilyInstance>()
                .Symbol.get_Parameter(paramGuid);

            return sourceFormParam;
        }
    }
}