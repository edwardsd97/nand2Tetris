(SimpleFunction.test)
@SP
A=M
M=0
@SP
AM=M+1
M=0
@SP
AM=M+1
@0
D=A
@LCL
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@1
D=A
@LCL
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@SP
AM=M-1
D=M
@SP
AM=M-1
M=D+M
@SP
M=M+1
@SP
AM=M-1
M=!M
@SP
M=M+1
@0
D=A
@ARG
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@SP
AM=M-1
D=M
@SP
AM=M-1
M=D+M
@SP
M=M+1
@1
D=A
@ARG
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@SP
AM=M-1
D=M
@SP
AM=M-1
M=M-D
@SP
M=M+1
@LCL
D=M
@R13
M=D
D=D-1
D=D-1
D=D-1
D=D-1
D=D-1
A=D
D=M
@R14
M=D
@SP
A=M-1
D=M
@ARG
A=M
M=D
@SP
M=M-1
@ARG
D=M+1
@SP
M=D
@R13
AM=M-1
D=M
@THAT
M=D
@R13
AM=M-1
D=M
@THIS
M=D
@R13
AM=M-1
D=M
@ARG
M=D
@R13
AM=M-1
D=M
@LCL
M=D
@R14
A=M
0;JMP
(_END)
@_END
0;JMP
