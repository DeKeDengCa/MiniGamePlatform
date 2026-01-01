using System.Text;
using NetworkFramework.Utils;
using Scommon;

namespace NetworkFramework.Core.Model
{
    
    /// <summary>
    /// 公共参数类
    /// </summary>
    public class PublicParams
    {
        public string VersionName { get; set; }
        public long VersionCode { get; set; }
        public string Did { get; set; }
        public string Lan { get; set; }
        public string CountryCode { get; set; }
        public string PackageName { get; set; }
        public Gender Gender { get; set; }
        public string Rel { get; set; }
        public string Cha { get; set; }
        public string SubCha { get; set; }
        public string Biz { get; set; }
        public string BizVer { get; set; }

        /// <summary>
        /// 编码方法
        /// </summary>
        public byte[] Encode(ContentType contentType)
        {
            
            var pubPara = new PubPara
            {
                Ver = VersionName,
                Verc = VersionCode,
                Did = Did,
                Lan = Lan,
                Pkg = PackageName,
                Cou = CountryCode,
                Gender = Gender,
                Rel = Rel,
                Cha = Cha,
                SubCha = SubCha,
                BizVer = BizVer,
                Biz = Biz
            };
            
            return contentType == ContentType.Proto
                ? Serializer.SerializeToProtoBuf(pubPara)
                : Encoding.UTF8.GetBytes(Serializer.SerializeToJson(pubPara));
            
        }

        /// <summary>
        /// 克隆公共参数
        /// </summary>
        public PublicParams Clone()
        {
            return new PublicParams
            {
                VersionName = this.VersionName,
                VersionCode = this.VersionCode,
                Did = this.Did,
                Lan = this.Lan,
                CountryCode = this.CountryCode,
                PackageName = this.PackageName,
                Gender = this.Gender,
                Rel = this.Rel,
                Cha = this.Cha,
                SubCha = this.SubCha,
                Biz = this.Biz,
                BizVer = this.BizVer
            };
        }
    }
}