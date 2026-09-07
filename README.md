# Rime 简体中文用户定制文件
以 [袖珍简化字拼音](https://github.com/rime/rime-pinyin-simp) 为基础，在此之上定制了适合简体中文用户使用习惯的定制文件。

**2025/05/27 更新**：现在有很多功能强大且开箱即用的项目，如 [雾凇拼音](https://github.com/iDvel/rime-ice)，没有很强定制需求推荐直接使用。

如果像我这样更想按自己的喜好去定制，用不到的功能绝对不加，那么也可以从这些优秀且成熟的项目中获得帮助。

在此对他们表示敬意。

## 为什么不用朙月拼音？

朙月拼音在输入简体中文时，是将繁体中文使用 [OpenCC](https://github.com/BYVoid/OpenCC) 转化成简体中文列在候选列表继而进行输入。

默认方案在简体中文使用场景时会出现一些小瑕疵。

如「乾」在组「乾坤」之外的词组后，再输入时会变成「干」；又或者输入「买」的时候会发现「𧹒」也在候选列表，而绝大部分场景不会用到这个字。

这些问题可能可以通过强大的定制能力去解决，但是对于一般的简体中文用户来说，使用以简体中文为出发点的方案会更好一些。

## 这个项目是做什么的？

Rime 为简体中文用户专门提供了袖珍简化字拼音，但是默认方案直接使用不够方便。本项目提供了作者个人使用的配置文件，帮助有意如此使用的用户快速上手。

**请注意，文件中包含了个人使用习惯，如不合适或不必要，请自行前往 [Rime 官方网站 ](https://rime.im/)查阅文档修改。**

## 文件组成

### 文件

- `pinyin_simp.custom.yaml` ：袖珍简化字拼音方案的自定义修改，包含初始输出状态、符号输入等功能修改，**同时指定了新词库中心文件**，如果需实现其他功能修改请自行增删。

  简繁切换借鉴自项目 [https://github.com/iDvel/rime-ice](https://github.com/iDvel/rime-ice)。

- `pinyin_simp.main.dict.yaml` ：中心词库文件，主要功能为引用其他词库，需在输入方案中指定方可使用。词库内容为 [袖珍简化字拼音](https://github.com/rime/rime-pinyin-simp) 默认方案词库修改而来，一般不做修改。

- `cn_en.dict.yaml` ：英文及中英混输词库，由中心词库文件引用使用。如需添加新的英文或中英混输词汇，建议编辑此文件。

- `pinyin_simp_custom.dict.yaml` ：自定义词语，由中心词库文件引用使用。如需添加自定义短语，建议编辑此文件。

- `pinyin_simp_pin.yaml` ：候选固定，使用 lua 函数来实现候选列表固定排序的功能，来源为项目 [https://github.com/iDvel/rime-ice](https://github.com/iDvel/rime-ice)。

### 文件夹

- ``cn_dicts`` ：第三方词库，由中心词库文件引用使用，来源为项目 [https://github.com/iDvel/rime-ice](https://github.com/iDvel/rime-ice)。
- ``lua`` ：lua 函数文件，目前仅使用候选固定排序的功能，来源为项目 [https://github.com/iDvel/rime-ice](https://github.com/iDvel/rime-ice)。
- ``gram``：输入法语法模型，来自万象语法模型，来源为项目 [https://github.com/amzxyz/RIME-LMDG](https://github.com/amzxyz/RIME-LMDG)。
- `windows`：Windows 自动任务工具的源码、测试和构建说明，仅在 `windows-automation` 分支维护。用户使用 Release 中的可执行程序，无需复制源码。

## 为什么会删除部分字词？

个人在使用过程中发现，部分只有声母的词语，会影响到只输入该声母时的候选列表，带来诸多不便。

考虑到这些字词的使用场景很小，故移除这部分字词以获得良好输入体验，如若不喜，可以自行加回。

移除的字词如下：

`pinyin_simp.main.dict.yaml` 词典：

```yaml
唔	n	
嗯	n	
唔	ng	
嗯	ng	
噷	hm	
呣	m	
嘸	m	
呒	m	
```

## 使用方法

### 安装使用

- 安装 Rime 输入法并安装 [袖珍简化字拼音](https://github.com/rime/rime-pinyin-simp) 方案，使用软件或修改 `default.custom.yaml` 文件加入该方案。
- 将上述文件复制到 Rime 用户文件夹内。
- 重新部署。
-  打开 Rime 方案选单，切换至「袖珍简化字拼音」即可开始使用。

### Windows 平台计划任务

Windows 自动任务工具提供独立的「自动同步」和「自动重新部署」开关，分别设置每日执行时间。到达计划时间时，电脑锁屏也照常执行。安装后可从开始菜单打开，计划每次执行时定位当前小狼毫版本，正常原地升级无需重新设置。

最终程序通过 [GitHub Releases](https://github.com/huaxianyan/Rime/releases) 发布，目前首版尚未发布。面向 Windows 10 22H2／Windows 11 的 x64 设备，使用系统包含的 .NET Framework 4.8，不依赖 PowerShell。

源码在 `windows-automation` 分支维护，详细安装、更新、卸载和开发说明见 [Windows 工具文档](windows/README.md)。本工具只处理 Windows 计划任务，其他平台如有需求将另行发布对应程序。

如果此前按照 [同步设置文章](https://mengxiangxi.info/BLOG/coding/2024/11/07/Weasels-setting-sync.html) 创建过旧任务，请先手动停用，再启用新计划，避免重复执行。

