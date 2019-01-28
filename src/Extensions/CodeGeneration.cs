using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using LinqToDB.Mapping;

namespace Linq2Db.SqlServer
{
    internal static class CodeGeneration
    {
        private static readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);

        private static readonly ConcurrentDictionary<Type, (Type, Expression, Delegate)> _ctTypes =
            new ConcurrentDictionary<Type, (Type, Expression, Delegate)>();

        public static (Type CtType, Expression Joiner, Delegate Mapper) GetCtTypeForEntity(this EntityDescriptor descriptor)
        {
            if (_ctTypes.TryGetValue(descriptor.ObjectType, out var ctType))
                return ctType;
            _locker.Wait();
            try
            {
                if (_ctTypes.TryGetValue(descriptor.ObjectType, out ctType))
                    return ctType;
                var typeOnly = GenerateCtTypeForEntity(descriptor);
                var ex = GenerateCtJoinExpression(descriptor,  typeOnly);
                var mapper = GenerateCtToEntityMapper(descriptor, typeOnly);
                ctType = (typeOnly, ex, mapper);
                _ctTypes.TryAdd(descriptor.ObjectType, ctType);
                return ctType;

            }
            finally
            {
                _locker.Release();
            }

        }


        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static Delegate GenerateCtToEntityMapper(this EntityDescriptor descriptor, Type ctType)
        {
            var entityType = descriptor.ObjectType ?? throw new ArgumentException("descriptor.ObjectType == null");

            var ctParam = Expression.Parameter(ctType, "ct");
            var createEntity = Expression.New(
                entityType.GetConstructor(new Type[] { }) ??
                           throw new NotSupportedException("No default constructor on entity"));
            var makeEntity = Expression.MemberInit(createEntity,
                descriptor.Columns.Where(p => p.IsPrimaryKey).Select(p =>

                    Expression.Bind(entityType.GetProperty(p.MemberName), Expression.MakeMemberAccess(ctParam,
                        ctType.GetProperty(p.MemberName)))));
            var map = Expression.Lambda(makeEntity, ctParam).Compile();
            return map;
        }


        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static Expression GenerateCtJoinExpression(this EntityDescriptor descriptor, Type ctType)
        {
            var entityType = descriptor.ObjectType ?? throw new ArgumentException("descriptor.ObjectType == null");
            var ctParam = Expression.Parameter(ctType, "ct");
            var eParam = Expression.Parameter(entityType, "e");
            Expression current = null;
            foreach (var pk in descriptor.Columns.Where(p => p.IsPrimaryKey))
            {
                var entityProperty = entityType.GetProperty(pk.MemberName);
                var ctProperty = ctType.GetProperty(pk.MemberName);
                var cmp = Expression.Equal(Expression.MakeMemberAccess(ctParam, ctProperty),
                    Expression.MakeMemberAccess(eParam, entityProperty));
                if (current == null)
                    current = cmp;
                else
                    current = Expression.AndAlso(current, cmp);
            }



            return Expression.Lambda(current, ctParam, eParam);
        }

        private static Type GenerateCtTypeForEntity(this EntityDescriptor descriptor)
        {
            var assemblyName = new AssemblyName($"linq2db.sqlserver.ct.{descriptor.TypeAccessor.Type.FullName}");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
            var typeBuilder = moduleBuilder.DefineType(descriptor.TypeAccessor.Type.Name + "Ct",
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout, typeof(CtBase));
            foreach (var pk in descriptor.Columns.Where(p => p.IsPrimaryKey))
            {
                CreateProperty(typeBuilder, pk.MemberName, pk.MemberType, pk.ColumnName);
            }
            var constructor = typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            return typeBuilder.CreateTypeInfo().AsType();

        }

        private static void CreateProperty(TypeBuilder tb, string propertyName, Type propertyType,
            string fieldName)
        {
            var fieldBuilder = tb.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            var getPropBuilder = tb.DefineMethod("get_" + propertyName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propertyType, Type.EmptyTypes);
            var getIl = getPropBuilder.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);
            var setPropBuilder = tb.DefineMethod("set_" + propertyName,
                MethodAttributes.Public |
                MethodAttributes.SpecialName |
                MethodAttributes.HideBySig,
                null, new[] {propertyType});
            var setIl = setPropBuilder.GetILGenerator();
            var modifyProperty = setIl.DefineLabel();
            var exitSet = setIl.DefineLabel();

            setIl.MarkLabel(modifyProperty);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);

            setIl.Emit(OpCodes.Nop);
            setIl.MarkLabel(exitSet);
            setIl.Emit(OpCodes.Ret);

            var columnAttrInfo = typeof(ColumnAttribute).GetConstructor(new[] { typeof(string) });
            var columnAttrBuilder = new CustomAttributeBuilder(columnAttrInfo, new object[] { fieldName });

            var propertyBuilder = tb.DefineProperty(propertyName, PropertyAttributes.HasDefault, propertyType, null);
            propertyBuilder.SetCustomAttribute(columnAttrBuilder);
            propertyBuilder.SetGetMethod(getPropBuilder);
            propertyBuilder.SetSetMethod(setPropBuilder);
        }
    }
}