# UnityEditorExpand
unity编辑器拓展



如果把编辑器代码放到工程里面会有几个问题
1功能报错，如果报错了你无法去改你的编辑器代码，改了也不能立刻得到验证 。
2可能会出现无法使用编辑器工具的情况，因为编译报错。
好处：
一处编译各处运行 ，利用vs的生成事件将dll拷贝到各个工程。

使用方式： 编译生成dll 然后利用xCopy指令拷贝dll到各个工程的Plusin/Editor目录下，注意该dll只能是编辑器模式下使用，不参与打包
放置dll的目录：
<img width="575" height="400" alt="image" src="https://github.com/user-attachments/assets/d2ef3a54-7265-42b9-84e3-875180a6df6b" />

如何生成dll到各个工程 ：
<img width="1570" height="527" alt="image" src="https://github.com/user-attachments/assets/434036db-41d4-4620-a14a-ae8354ec9fb3" />

csdn链接：
https://blog.csdn.net/yangjie6898862/article/details/147554292?spm=1001.2014.3001.5502
