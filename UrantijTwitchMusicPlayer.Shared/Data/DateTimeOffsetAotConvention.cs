using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace UrantijTwitchMusicPlayer.Shared.Data;

// ЭТО ВАЙБКОД)))
public class DateTimeOffsetAotConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder,
        IConventionContext<IConventionModelBuilder> context)
    {
        // Создаем инстансы один раз
        var defaultComparer = new ValueComparer<DateTimeOffset>((c1, c2) => c1 == c2, c => c.GetHashCode(), c => c);

        // Бежим по всем сущностям и свойствам в поисках DateTimeOffset
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    // Задаем конвертер и компаратор напрямую в метаданные свойства
                    // property.SetValueConverter(identityConverter);
                    property.SetValueComparer(defaultComparer);
                }
            }
        }
    }
}