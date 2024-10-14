using Models.Database.Collections;
using Models.Database.General;

namespace API.Features.Storage.Models;

public class HierarchicalCollection(Collection collection, Hierarchy hierarchy)
{
    public Collection Collection { get; set; } = collection;
    
    public Hierarchy Hierarchy { get; set; } = hierarchy;
}