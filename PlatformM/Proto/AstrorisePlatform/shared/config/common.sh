# 定义语言数组
LANGUAGES=("zh" "en")
#LANGUAGES=("zh" "en" "ar")
DEFAULT_LANG="en"

set -e  # 遇到错误退出

# 函数：打印带颜色的日志
log_info() {
    echo -e "\033[32m[INFO] $1\033[0m"
}

log_warning() {
    echo -e "\033[33m[WARN] $1\033[0m"
}

log_error() {
    echo -e "\033[31m[ERROR] $1\033[0m"
    exit 1
}

# 检查环境变量是否存在
if [ -z "${CONFIGX_WORKSPACE}" ]; then
    log_error "缺少环境变量 CONFIGX_WORKSPACE"
fi
#if [ -z "${AR_SERVER_WORKSPACE}" ]; then
#    log_error "缺少环境变量 AR_SERVER_WORKSPACE"
#fi

# # 解析参数
# while [[ "$#" -gt 0 ]]; do
#     case "$1" in
#         -lang|--language)
#             LANG_ARG="$2"
#             shift 2
#             ;;
#         *)
#             echo "未知参数: $1"
#             exit 1
#             ;;
#     esac
# done

lang="$DEFAULT_LANG"
# 检查是否有 -lang 参数
if [ -n "$LANG_ARG" ]; then
    # 检查参数是否在数组中
    if [[ " ${LANGUAGES[*]} " =~ " $LANG_ARG " ]]; then
	lang="$LANG_ARG"
    else
        log_error "不支持的语言 '$LANG_ARG'，合法选项: ${LANGUAGES[*]}"
    fi
    echo "合法语言: $lang"
#else
#    # 提示用户输入
#    read -p "请输入语言(默认: $DEFAULT_LANG): " USER_INPUT
#    # 如果用户直接回车，则使用默认值
#    if [ -z "$USER_INPUT" ]; then
#        USER_INPUT="$DEFAULT_LANG"
#    fi
#    # 检查输入是否在数组中
#    if [[ " ${LANGUAGES[*]} " =~ " $USER_INPUT " ]]; then
#	lang="$USER_INPUT"
#    else
#        log_error "不支持的语言 '$USER_INPUT'，合法选项: ${LANGUAGES[*]}"
#    fi
fi

# 检查必要文件
check_prerequisites() {
    if [ ! -f "$LUBAN_DLL" ]; then
        log_error "Luban.dll 不存在: $LUBAN_DLL"
    fi

    if [ ! -f "$CONF_ROOT/luban.conf" ]; then
        log_error "配置文件不存在: $CONF_ROOT/luban.conf"
    fi
}

# 获取当前目录的父目录的路径
get_parent_path() {
    local current_path=$(pwd)
    local target_dir="$1"
    if [[ "$current_path" == *"/$target_dir/"* ]]; then
        echo "${current_path%/$target_dir/*}/$target_dir"
    elif [[ "$current_path" == *"/$target_dir" ]]; then
        echo "$current_path"
    else
        log_error "目录 '$target_dir' 未找到"
        return 1
    fi
}