#!/bin/bash

# 默认配置
REPO_SOURCE="https://gitit.cc/social/protocol.git"
REPO_TARGET="https://gitit.cc/astrorise/shared/protocol.git"
TEMP_DIR="./tmp/git_sync_$(date +%s)"
BRANCH_SOURCE="dev"
BRANCH_TARGET="dev"

# 路径映射 - 使用兼容的数组定义
SRC_PATHS=("google" "protobuf")
DEST_PATHS=("google" "protobuf")

# 检测shell类型并定义路径映射
if [ -n "$ZSH_VERSION" ]; then
  # zsh
  typeset -A PATH_MAPPING
  PATH_MAPPING=(
    "google" "google"
    "protobuf" "protobuf"
  )
elif [ -n "$BASH_VERSION" ]; then
  # bash
  if [[ "$(declare -p PATH_MAPPING 2>/dev/null)" =~ "declare -A" ]]; then
    # bash 4.0+ 支持关联数组
    declare -A PATH_MAPPING
    PATH_MAPPING=(
      ["google"]="google"
      ["protobuf"]="protobuf"
    )
  else
    # 旧版bash不支持关联数组，使用索引数组
    PATH_MAPPING_TYPE="indexed"
  fi
else
  # 其他shell，使用索引数组
  PATH_MAPPING_TYPE="indexed"
fi

# 颜色输出
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# 清理函数
cleanup() {
    log_info "清理临时文件: $TEMP_DIR"
    rm -rf "$TEMP_DIR"
}

# 错误处理
set -e
trap cleanup EXIT

# 路径同步函数 - 兼容zsh和bash
sync_paths() {
    local changes_detected=false

    # 检测shell类型并使用相应的循环方法
    if [ -n "$ZSH_VERSION" ]; then
        # zsh 方式
        for src_path in ${(k)PATH_MAPPING}; do
            dest_path="${PATH_MAPPING[$src_path]}"
            if _sync_single_path "$src_path" "$dest_path"; then
                changes_detected=true
            fi
        done
    elif [ "$PATH_MAPPING_TYPE" = "indexed" ] || [ -z "$PATH_MAPPING" ]; then
        # bash 索引数组方式或其他shell
        for i in "${!SRC_PATHS[@]}"; do
            src_path="${SRC_PATHS[i]}"
            dest_path="${DEST_PATHS[i]}"
            if _sync_single_path "$src_path" "$dest_path"; then
                changes_detected=true
            fi
        done
    else
        # bash 关联数组方式
        for src_path in "${!PATH_MAPPING[@]}"; do
            dest_path="${PATH_MAPPING[$src_path]}"
            if _sync_single_path "$src_path" "$dest_path"; then
                changes_detected=true
            fi
        done
    fi

    # 返回是否检测到更改
    if [ "$changes_detected" = true ]; then
        return 0  # 0表示true，有更改
    else
        return 1  # 非0表示false，无更改
    fi
}

# 单个路径同步函数，返回是否成功同步
_sync_single_path() {
    local src_path="$1"
    local dest_path="$2"

    log_info "同步路径: $src_path -> $dest_path"

    # 确保目标目录存在
    mkdir -p "repoT/$(dirname "$dest_path")"

    # 删除目标路径的现有内容
    rm -rf "repoT/$dest_path"

    # 复制新内容
    if [ -d "repoS/$src_path" ]; then
        cp -r "repoS/$src_path" "repoT/$dest_path"
        log_info "成功同步: $src_path -> $dest_path"
        return 0  # 成功同步，返回true
    else
        log_warn "仓库A中路径 '$src_path' 不存在"
        return 1  # 未同步，返回false
    fi
}

main() {
    log_info "开始同步仓库内容..."

    # 创建临时目录
    mkdir -p "$TEMP_DIR"
    cd "$TEMP_DIR"

    # 克隆仓库
    log_info "克隆源仓库..."
    if ! git clone --branch "$BRANCH_SOURCE" "$REPO_SOURCE" repoS 2>/dev/null; then
        log_error "克隆源仓库失败"
        exit 1
    fi

    log_info "克隆目标仓库..."
    if ! git clone --branch "$BRANCH_TARGET" "$REPO_TARGET" repoT 2>/dev/null; then
        log_error "克隆目标仓库失败"
        exit 1
    fi

    # 同步路径
    if sync_paths; then
        sync_changes_detected=true
    else
        sync_changes_detected=false
    fi

    # 进入目标仓库目录
    cd repoT

    # 检查是否有更改
    local changes_detected=false
    if ! git diff --quiet || ! git diff --staged --quiet; then
        changes_detected=true
    fi

    if [ "$changes_detected" = false ] || [ "$sync_changes_detected" = false ]; then
        log_info "没有检测到更改，跳过提交"
        return 0
    fi

    # 提交更改
    git add .
    commit_message="自动同步: $(date '+%Y-%m-%d %H:%M:%S')"
    if git commit -m "$commit_message"; then
        log_info "提交成功: $commit_message"
    else
        log_error "提交失败"
        return 1
    fi

    # 推送更改
    log_info "推送到目标仓库..."
    if git push origin "$BRANCH_TARGET"; then
        log_info "同步完成!"
    else
        log_error "推送失败"
        return 1
    fi
}

# 记录初始目录
originPath=$(pwd)
# 进入脚本目录
cd "$(dirname "$0")" || exit

# 运行主函数
main "$@"

# 回到初始目录
cd "$originPath" || exit