using System.Net;
using System.Text.RegularExpressions;
using Turbo.Auth.Contracts.Media;
namespace Turbo.Auth.Application.Media;
public static class APIMartImagePolicy
{
    public static readonly string[] Models = ["gpt-image-2.5-flare","gpt-image-2.5-sunburst","gpt-image-2.6-flare","gpt-image-2.6-sunburst","gpt-image-2.5-ext","gpt-image-2.6-ext"];
    private static readonly string[] Ratios = ["1:1","16:9","9:16","4:3","3:4","3:2","2:3","5:4","4:5","21:9"];
    private static readonly string[] MoreRatios = ["9:21","2:1","1:2","3:1","1:3"];
    private static void Check(bool condition,string field) { if(!condition) throw new ImageRequestException(field); }
    private static bool PublicUrl(string value)
    {
        if (!Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        var host=uri.IdnHost;
        if(host.Equals("localhost",StringComparison.OrdinalIgnoreCase)||host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)||host.EndsWith(".internal",StringComparison.OrdinalIgnoreCase))return false;
        if(!IPAddress.TryParse(host,out var address))return host.Contains('.');
        if(IPAddress.IsLoopback(address)||address.IsIPv6LinkLocal||address.IsIPv6SiteLocal)return false;
        if(address.IsIPv4MappedToIPv6)address=address.MapToIPv4();
        var bytes=address.GetAddressBytes();
        return bytes.Length==16 ? (bytes[0]&0xfe)!=0xfc : bytes[0] is not(0 or 10 or 127) && !(bytes[0]==169&&bytes[1]==254) && !(bytes[0]==172&&bytes[1]>=16&&bytes[1]<=31) && !(bytes[0]==192&&bytes[1]==168) && bytes[0]<224;
    }
    private static bool DataImage(string value,int limit,bool pngOnly=false)
    {
        var comma=value.IndexOf(',');
        if(comma<0 || comma>40)return false;
        var kind=value[..comma];
        var kinds=pngOnly ? new[]{"data:image/png;base64"}:new[]{"data:image/png;base64","data:image/jpeg;base64","data:image/webp;base64","data:image/gif;base64","data:image/bmp;base64"};
        if(!kinds.Contains(kind)||value.Length-comma-1 > ((long)limit+2)/3*4)return false;
        try { var bytes=Convert.FromBase64String(value[(comma+1)..]); return bytes.Length>0&&bytes.Length<=limit; } catch(FormatException){return false;}
    }
    private static void Reference(string value,string field,bool allowData,int limit,bool pngOnly=false,bool httpsOnly=false)
    {
        Check(!string.IsNullOrWhiteSpace(value)&&(allowData&&DataImage(value,limit,pngOnly)||PublicUrl(value)&&(!httpsOnly||value.StartsWith("https://",StringComparison.OrdinalIgnoreCase))),field);
    }
    public static Dictionary<string,object> Build(APIMartImageGenerationRequest request)
    {
        Check(Models.Contains(request.Model),"model");
        Check(!string.IsNullOrWhiteSpace(request.Prompt)&&request.Prompt.Length<=32000,"prompt");
        Check(request.N>=1&&request.N<=4,"n");
        Check(request.IdempotencyKey is null||Regex.IsMatch(request.IdempotencyKey,"^[A-Za-z0-9_-]{8,128}$"),"idempotency_key");
        var ext25=request.Model=="gpt-image-2.5-ext";
        var ext26=request.Model=="gpt-image-2.6-ext";
        var ext=ext25||ext26;
        var newer=request.Model.Contains("2.6",StringComparison.Ordinal);
        var format=(request.OutputFormat??"png").Trim().ToLowerInvariant();
        if(ext26&&format=="jpg")format="jpeg";
        Check((newer?new[]{"png","jpeg","webp","gif","svg"}:new[]{"png","jpeg","webp"}).Contains(format),"output_format");
        var payload=new Dictionary<string,object>{["model"]=request.Model,["prompt"]=request.Prompt,["n"]=request.N};
        void Add(string field,object? value){if(value is not null)payload[field]=value;}
        if(ext)
        {
            var version=(request.Version??"flare").Trim().ToLowerInvariant(); Check(version is "flare" or "sunburst","version"); Add("version",version);
        }
        else Check(request.Version is null,"version");
        if(ext26){Check(request.N==1,"n");Check(request.FrameCount is null&&request.FrameDurationMs is null,"frame_count");}
        var resolution=request.Resolution??(ext25?"1K":"1k");
        if(ext26)resolution=resolution.Trim().ToLowerInvariant();
        Check((ext25?new[]{"1K","2K","4K"}:new[]{"1k","2k","4k"}).Contains(resolution),"resolution");
        if(ext26)Check(resolution=="1k","resolution");
        var size=request.Size;
        if(ext26&&size is not null)size=size.Trim().ToLowerInvariant();
        if(size is not null)
        {
            if(format=="gif")
            {
                if(ext26)Check(size=="auto","size");
                else if(size!="auto")GifSize(size,request.FrameCount??9);
            }
            else if(size!="auto"&&!Ratios.Contains(size))
            {
                if(ext)Check(false,"size");
                else if(!MoreRatios.Contains(size))StillSize(size);
            }
            Add("size",size);
        }
        if(size is null||!size.Contains('x'))Add("resolution",resolution);
        var references=request.ImageUrls?.ToArray()??[];
        Check(references.Length<=16,"image_urls");
        foreach(var image in references)Reference(image,"image_urls",ext,20*1024*1024);
        var editing=references.Length>0;
        if(editing)Add("image_urls",references);
        if(ext25)
        {
            Check(request.OutputFormat is null or "png","output_format");
            Check(request.Quality is null&&request.OutputCompression is null&&request.Background is null&&request.Moderation is null&&request.Complexity is null&&request.MaskUrl is null&&request.MarkupUrl is null&&request.FrameCount is null&&request.FrameDurationMs is null&&request.Svg is null,"unsupported_option");
            return payload;
        }
        var quality=request.Quality;
        if(ext26&&quality is not null)quality=quality.Trim().ToLowerInvariant();
        Check(quality is null||new[]{"auto","low","medium","high","xhigh","max"}.Contains(quality),"quality");
        Add("quality",quality); Add("output_format",format);
        if(newer)
        {
            var complexity=ext26?request.Complexity?.Trim().ToLowerInvariant():request.Complexity;
            Check(complexity is null or "low" or "medium" or "high","complexity"); Add("complexity",complexity);
        }
        else Check(request.Complexity is null,"complexity");
        Check(request.MaskUrl is null||editing,"mask_url"); Check(request.MarkupUrl is null||editing,"markup_url");
        Check(request.MaskUrl is null||request.MarkupUrl is null,"markup_url");
        if(ext26)Check(request.MaskUrl is null&&request.MarkupUrl is null,"unsupported_option");
        if(request.MaskUrl is not null){Reference(request.MaskUrl,"mask_url",true,4*1024*1024,true);Add("mask_url",request.MaskUrl);}
        if(request.MarkupUrl is not null){Check(newer,"markup_url");Reference(request.MarkupUrl,"markup_url",true,50*1024*1024,true,true);Add("markup_url",request.MarkupUrl);}
        if(!ext26)
        {
            Check(request.Background is null or "transparent" or "opaque" or "auto","background");
            Check(request.Background!="transparent"||format is "png" or "webp","background");
            Check(request.Moderation is null or "auto" or "low","moderation");
            Check(request.OutputCompression is null||format is "jpeg" or "webp","output_compression");
            Check(request.OutputCompression is null or >=0 and <=100,"output_compression");
            Add("background",request.Background); Add("moderation",request.Moderation??"low"); Add("output_compression",request.OutputCompression);
        }
        // Ext2.6 documents these as ignored: deliberately do not forward misleading options.
        if(format is "gif" or "svg")Check(!editing&&request.MaskUrl is null&&request.MarkupUrl is null,"image_urls");
        if(format=="gif")
        {
            Check(request.Svg is null,"svg"); Check(request.FrameCount is null or >=2 and <=16,"frame_count");Check(request.FrameDurationMs is null or >=20 and <=655350,"frame_duration_ms");
            Add("frame_count",request.FrameCount);Add("frame_duration_ms",request.FrameDurationMs);
        }
        else Check(request.FrameCount is null&&request.FrameDurationMs is null,"frame_count");
        if(format=="svg")
        {
            Check(request.N==1,"n"); Check(request.Background is null,"background");
            Check(ext26||request.Svg is not null,"svg");
            if(request.Svg is not null)
            {
                Check(!string.IsNullOrWhiteSpace(request.Svg.Model)&&request.Svg.Model.Length<=200,"svg.model");
                var svg=new Dictionary<string,object>{["model"]=request.Svg.Model};
                var effort=request.Svg.Reasoning?.Effort;
                if(ext26)effort=effort?.Trim().ToLowerInvariant();
                Check(effort is null||new[]{"none","minimal","low","medium","high","xhigh","max"}.Contains(effort),"svg.reasoning.effort");
                if(effort is not null)svg["reasoning"]=new{effort}; Add("svg",svg);
            }
        }
        else Check(request.Svg is null,"svg");
        return payload;
    }
    private static (int Width,int Height) Dimensions(string size)
    {
        Check(Regex.IsMatch(size,"^[1-9][0-9]{0,4}x[1-9][0-9]{0,4}$"),"size");
        var parts=size.Split('x');var width=int.Parse(parts[0]);var height=int.Parse(parts[1]);
        Check(width%16==0&&height%16==0,"size");return(width,height);
    }
    private static void StillSize(string size)
    {
        var(w,h)=Dimensions(size);Check(w<=3840&&h<=3840&&w<=3L*h&&h<=3L*w&&(long)w*h>=655360&&(long)w*h<=8294400,"size");
    }
    private static void GifSize(string size,int count)
    {
        Check(count>=2&&count<=16,"frame_count");var(w,h)=Dimensions(size);
        var(cols,rows)=count<=8?(4,2):count==9?(3,3):count<=12?(4,3):(4,4);
        bool Fits(int c,int r){var sw=(long)w*c;var sh=(long)h*r;return sw<=3840&&sh<=3840&&sw*sh<=8294400&&sw<=3*sh&&sh<=3*sw;}
        Check(Fits(cols,rows)||Fits(rows,cols),"size");
    }
}
