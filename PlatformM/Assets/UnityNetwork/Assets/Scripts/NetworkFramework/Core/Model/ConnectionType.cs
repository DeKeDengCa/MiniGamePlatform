namespace NetworkFramework.Core.Model
{
    
    /// <summary>
    /// 连接类型 - 与Android端保持一致
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>
        /// 长连接
        /// </summary>
        PERSISTENT = 1,
        
        /// <summary>
        /// 短连接
        /// </summary>
        INCONSTANT = 2,
        
        /// <summary>
        /// 长连接优先
        /// </summary>
        PERSISTENT_PRECEDE = 3,
        
        /// <summary>
        /// 短连接优先
        /// </summary>
        INCONSTANT_PRECEDE = 4
    }

}