namespace Turbo_Auth.Handlers.Model;

/// on 模型还原器，当一个模型被设定在另外一个框架上的时候，可以依据一定的规则还原
/// 原来的模型 
public class PlayMixModelBacker
{
    public string Backer(string model)
    {
        if (model.EndsWith("_o"))
        {
            return model[..^2];
        }

        if (model.EndsWith("_g"))
        {
            return model[..^2];
        }

        return model;
    }
}