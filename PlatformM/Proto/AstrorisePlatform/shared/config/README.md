# 游戏配置生成说明

### 构建运行环境

首先你得确保本地拉取了`configx`项目，该项目地址为：

```textile
https://gitit.cc/astrorise/shared/configx.git
```

拉下来后，根据该项目的`README.md`进行操作完成配置，这个操作只需要执行一次。

### 配置你的Excel表格

xlsx文件在`Datas`目录下，一般程序已经完成了初版的配置，在原框架下新增或修改数据即可。如果还需要更多配置表用法和技巧，可以查看`luban`官网的相关文档。

### 生成游戏配置

一般来说，编辑完配置表后提交即可，程序会自己执行脚本然后导入配置到项目，但很多时候配置的表格可能会有格式错误，这时候执行一下生成脚本，完成内容检查。执行后，有错则改，无错则提交配置。

```shell
# 前端生成脚本
sh gen_cli.sh

# 后端生成脚本
sh gen_svr.sh
```

### 关于多语言

多语言配置`lang.xlsx`文件会按语言种类生成多个语言文件。按现有配置，直接在`content@zh`和`content@en`列内填写内容即可。

| ##var   | id          | desc   | content | [content@zh](mailto:content@zh) | [content@en](mailto:content@en) |
| ------- | ----------- | ------ | ------- | ------------------------------- | ------------------------------- |
| ##type  | string      | string | string  |                                 |                                 |
| ##group | c           | none   | c       |                                 |                                 |
| ##      | id          | 描述     |         | 中文                              | 英文                              |
|         | lang_hello  | 描述1    |         | 你好                              | hello                           |
|         | lang_sorry  | 描述2    |         | 抱歉                              | sorry                           |
|         | lang_attack | 描述3    |         | 进攻                              | attack                          |

如果要新加新的语种，除了在Excel表格内新加列`content@xx`，还需要在`common.sh`脚本里增加导出语种支持。

```shell
# 定义语言数组
LANGUAGES=("zh" "en") # 默认只有zh和en
# 添加新的语言
LANGUAGES=("zh" "en" "xx") # 加到这里面
```
