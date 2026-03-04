namespace Iacula.Infrastructure.EntityFramework.Profiles;

using AutoMapper;
using DbEntity = Iacula.Infrastructure.EntityFramework.Models.FormDbEntity;
using Entity = Iacula.Domain.Entities.FormEntity;

internal sealed class FormDbEntityProfile : Profile
{
    public FormDbEntityProfile()
    {
        base.CreateMap<Entity, DbEntity>()
            .ForMember(target => target.Id, opt => opt.MapFrom(source => source.Id.Value))
            .ForMember(target => target.Payload, opt => opt.MapFrom(source => source.Payload))
            .ForMember(target => target.Status, opt => opt.MapFrom(source => (int)source.Status))
            .ForMember(target => target.Version, opt => opt.MapFrom(source => source.Version))
            ;

        CreateMap<DbEntity, Entity>()
            .ConstructUsing(source => new Entity(new FormId(source.Id), source.Payload, (MessageStatus)source.Status, source.Version))
            ;
    }
}
