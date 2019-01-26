using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using LinqToDB.Linq;
using LinqToDB.Mapping;

namespace Linq2Db.SqlServer.ChangeTracking
{
    public static class CodeGeneration
    {
        private static readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);
            private static ConcurrentDictionary<Type, (Type, Expression, object)> _ctTypes = new ConcurrentDictionary<Type, (Type, Expression, object)>();

        /*public static LambdaExpression CitateParameter(LambdaExpression expression, string paramName,
            ParameterExpression toParam)
        {
            var paramToChange = expression.Parameters.First(p => p.Name == paramName);
            var changed =  new CitateParameterVisitor(paramToChange,toParam).Visit(expression.Body);
            
        }

        private class CitateParameterVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _old;
            private readonly Expression _newEx;

            public CitateParameterVisitor(ParameterExpression old, Expression newEx)
            {
                _old = old;
                _newEx = newEx;
            }

            public override Expression Visit(Expression node)
            {
                return node == _old ? _newEx : base.Visit(node);
            }
        }*/
        
        public static (Type, Expression, object) GetCtTypeForEntity(this EntityDescriptor descriptor)
        {
            if (_ctTypes.TryGetValue(descriptor.ObjectType, out var ctType))
                return ctType;
            _locker.Wait();
            try
            {
                if (_ctTypes.TryGetValue(descriptor.ObjectType, out ctType))
                    return ctType;
                var typeOnly = DoGenerateCtTypeForEntity(descriptor);
                var (ex, map) = DoGenerateCtJoinExpressionCt(descriptor, descriptor.ObjectType, typeOnly);
                ctType = (typeOnly, ex, map);
                _ctTypes.TryAdd(descriptor.ObjectType, ctType);
                return ctType;

            }
            finally
            {
                _locker.Release();
            }

            
        }

        private static (Expression, object) DoGenerateCtJoinExpressionCt(this EntityDescriptor descriptor,
            Type entityType, Type ctType)
        {
            var ctParam = Expression.Parameter(ctType, "ct");
            var eParam = Expression.Parameter(entityType, "e");
            Expression current = null;
            foreach (var pk in descriptor.Columns.Where(p => p.IsPrimaryKey))
            {
                var emember = entityType.GetProperty(pk.MemberName);
                var ctmember = ctType.GetProperty(pk.MemberName);
                var cmp = Expression.Equal(Expression.MakeMemberAccess(ctParam, ctmember),
                    Expression.MakeMemberAccess(eParam, emember));
                if (current == null)
                    current = cmp;
                else
                    current = Expression.AndAlso(current, cmp);
            }

            var createEntity = Expression.New(entityType.GetConstructor(new Type[] { }));
            var makeEntity = Expression.MemberInit(createEntity,
                descriptor.Columns.Where(p => p.IsPrimaryKey).Select(p =>
                    Expression.Bind(entityType.GetProperty(p.MemberName), Expression.MakeMemberAccess(ctParam,
                        ctType.GetProperty(p.MemberName)))));
            var map = Expression.Lambda(makeEntity, ctParam).Compile();
            
            return (Expression.Lambda(current, ctParam, eParam), map);
        }

        private static Type DoGenerateCtTypeForEntity(this EntityDescriptor descriptor)
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