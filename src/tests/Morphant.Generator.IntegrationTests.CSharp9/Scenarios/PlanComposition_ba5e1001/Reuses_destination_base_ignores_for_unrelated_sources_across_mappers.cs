// Compiled integration scenario: TypeMapperInheritanceTests/PlanCompositionTests::Reuses_destination_base_ignores_for_unrelated_sources_across_mappers
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PlanComposition_ba5e1001
{
    public abstract class BaseEntity
    {
        public Guid Id { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime ModifiedOn { get; set; }

        public bool IsDeleted { get; set; }
    }

    public sealed class CustomerEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class OrderEntity : BaseEntity
    {
        public string Number { get; set; } = string.Empty;
    }

    public sealed class CreateCustomerModel
    {
        public Guid Id { get; init; }

        public DateTime CreatedOn { get; init; }

        public DateTime ModifiedOn { get; init; }

        public bool IsDeleted { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public struct ImportOrderRow
    {
        public Guid Id { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime ModifiedOn { get; set; }

        public bool IsDeleted { get; set; }

        public string Number { get; set; }
    }

    public abstract class EntityMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : EntityMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<object, BaseEntity>()
                .Members(source => new()
                {
                    Id = Ignore(),
                    CreatedOn = Ignore(),
                    ModifiedOn = Ignore(),
                    IsDeleted = Ignore()
                });
    }

    [MorphantMapper]
    public partial class CustomerMapper : EntityMapper<CustomerMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<CreateCustomerModel, CustomerEntity>()
                .IncludeBase<object, BaseEntity>();
        }
    }

    [MorphantMapper]
    public partial class OrderMapper : EntityMapper<OrderMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<ImportOrderRow, OrderEntity>()
                .IncludeBase<object, BaseEntity>();
        }
    }

    public static class Scenario
    {
        private static readonly DateTime SourceCreatedOn =
            new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc);

        private static readonly DateTime SourceModifiedOn =
            new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc);

        private static readonly DateTime ExistingCreatedOn =
            new DateTime(2020, 3, 4, 5, 6, 7, DateTimeKind.Utc);

        private static readonly DateTime ExistingModifiedOn =
            new DateTime(2021, 4, 5, 6, 7, 8, DateTimeKind.Utc);

        public static void Verify()
        {
            VerifyReferenceSource();
            VerifyValueSource();
        }

        private static void VerifyReferenceSource()
        {
            var source = new CreateCustomerModel
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CreatedOn = SourceCreatedOn,
                ModifiedOn = SourceModifiedOn,
                IsDeleted = true,
                Name = "Ada"
            };
            var mapper =
                (ITypeMapper<CreateCustomerModel, CustomerEntity>)
                new CustomerMapper();

            var created = mapper.Create(source, default);
            AssertCreatedBaseState(created, "customer");

            if (created.Name != "Ada")
            {
                throw new InvalidOperationException(
                    "The customer-specific member was not mapped.");
            }

            var existingId =
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var existing = new CustomerEntity
            {
                Id = existingId,
                CreatedOn = ExistingCreatedOn,
                ModifiedOn = ExistingModifiedOn,
                IsDeleted = false,
                Name = "before"
            };
            var updated = mapper.Update(source, existing, default);

            AssertPreservedBaseState(
                updated,
                existingId,
                ExistingCreatedOn,
                ExistingModifiedOn,
                false,
                "customer");

            if (!ReferenceEquals(updated, existing) || updated.Name != "Ada")
            {
                throw new InvalidOperationException(
                    "The customer entity was not updated normally.");
            }
        }

        private static void VerifyValueSource()
        {
            var source = new ImportOrderRow
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                CreatedOn = SourceCreatedOn,
                ModifiedOn = SourceModifiedOn,
                IsDeleted = true,
                Number = "ORD-42"
            };
            var mapper =
                (ITypeMapper<ImportOrderRow, OrderEntity>)new OrderMapper();

            var created = mapper.Create(source, default);
            AssertCreatedBaseState(created, "order");

            if (created.Number != "ORD-42")
            {
                throw new InvalidOperationException(
                    "The order-specific member was not mapped.");
            }

            var existingId =
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var existing = new OrderEntity
            {
                Id = existingId,
                CreatedOn = ExistingCreatedOn,
                ModifiedOn = ExistingModifiedOn,
                IsDeleted = false,
                Number = "before"
            };
            var updated = mapper.Update(source, existing, default);

            AssertPreservedBaseState(
                updated,
                existingId,
                ExistingCreatedOn,
                ExistingModifiedOn,
                false,
                "order");

            if (!ReferenceEquals(updated, existing) ||
                updated.Number != "ORD-42")
            {
                throw new InvalidOperationException(
                    "The order entity was not updated normally.");
            }
        }

        private static void AssertCreatedBaseState(
            BaseEntity entity,
            string mapping)
        {
            if (entity.Id != Guid.Empty ||
                entity.CreatedOn != default ||
                entity.ModifiedOn != default ||
                entity.IsDeleted)
            {
                throw new InvalidOperationException(
                    $"The shared ignores were not applied while creating " +
                    $"the {mapping} entity.");
            }
        }

        private static void AssertPreservedBaseState(
            BaseEntity entity,
            Guid id,
            DateTime createdOn,
            DateTime modifiedOn,
            bool isDeleted,
            string mapping)
        {
            if (entity.Id != id ||
                entity.CreatedOn != createdOn ||
                entity.ModifiedOn != modifiedOn ||
                entity.IsDeleted != isDeleted)
            {
                throw new InvalidOperationException(
                    $"The shared ignores were not applied while updating " +
                    $"the {mapping} entity.");
            }
        }
    }
}
