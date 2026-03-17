namespace Iacula.Infrastructure.EntityFramework.Profiles;

using AutoMapper;
using DbEntity = Iacula.Infrastructure.EntityFramework.Models.FormDbEntity;
using Entity = Iacula.Domain.Entities.FormEntity;

internal sealed class FormDbEntityProfile : Profile
{
    public FormDbEntityProfile()
    {
        base.CreateMap<Entity, DbEntity>()
            .ForMember(target => target.Id, options => options.MapFrom(source => source.Id.Value))
            .ForMember(target => target.Payload, options => options.MapFrom(source => source.Payload))
            .ForMember(target => target.Status, options => options.MapFrom(source => (int)source.Status))
            .ForMember(target => target.Version, options => options.MapFrom(source => source.Version))
            ;

        base.CreateMap<DbEntity, Entity>()
            .ConstructUsing(source => new Entity(new FormId(source.Id), source.Payload, (MessageStatus)source.Status, source.Version))
            ;
    }
}
