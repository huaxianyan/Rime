# Rime 简体中文用户定制文件
以 [袖珍简化字拼音](https://github.com/rime/rime-pinyin-simp) 为基础，在此之上定制了适合简体中文用户使用习惯的定制文件。

## 使用指导

- `custom_phrase.txt` ：自定义短语，目前用于固定常用单字词频，给足够大的权重即可。

- `pinyin_simp.custom.yaml` ：袖珍简化字拼音方案的自定义修改，包含初始输出状态、符号输入等功能修改。如想实现更多请访问 [Rime 官方网站](https://rime.im/) 翻阅相应文档。

- `pinyin_simp.extended.dict.yaml` ：输入方案的额外词库，需配合自定义修改中指定使用，为引用其他词库的中心文件。

- `pinyin_simp_base.dict.yaml` ：输入方案的基础词库，由额外词库文件引用使用，来源为项目 [https://github.com/alswl/Rime](https://github.com/alswl/Rime) 中的「现代汉语常用词表」，一般不做修改。

- `pinyin_simp_cn_en.dict.yaml` ：输入方案的英文及中英混输词库，由额外词库文件引用使用。如需添加新的英文或中英混输词汇，建议编辑此文件。

- `pinyin_simp_custom.dict.yaml` ：输入方案的自定义输入习惯词库，由额外词库文件引用使用。如需添加额外的自定义输入习惯，建议编辑此文件。此文件功能上基本等同于自定义短语，因将 `custom_phrase.txt` 单独用作单字词频调整，所以额外使用该文件来整理自定义输入习惯。