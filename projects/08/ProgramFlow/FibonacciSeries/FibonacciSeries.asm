@256
D=A
@SP
M=D
@Boot.Sys.init$ret.0
D=A
@SP
A=M
M=D
@SP
M=M+1
@LCL
D=M
@SP
A=M
M=D
@SP
M=M+1
@ARG
D=M
@SP
A=M
M=D
@SP
M=M+1
@THIS
D=M
@SP
A=M
M=D
@SP
M=M+1
@THAT
D=M
@SP
A=M
M=D
@SP
M=M+1
@SP
D=M-1
D=D-1
D=D-1
D=D-1
D=D-1
@ARG
M=D
@SP
D=M
@LCL
M=D
@Sys.init
0;JMP
(Boot.Sys.init$ret.0)
@1
D=A
@ARG
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@1
D=A
@THIS
D=D+A
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@0
D=A
@SP
M=M+1
A=M-1
M=D
@0
D=A
@THAT
D=D+M
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@1
D=A
@SP
M=M+1
A=M-1
M=D
@1
D=A
@THAT
D=D+M
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@0
D=A
@ARG
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@2
D=A
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
@0
D=A
@ARG
D=D+M
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
(MAIN_LOOP_START)
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
@COMPUTE_ELEMENT
D;JNE
@END_PROGRAM
0;JMP
(COMPUTE_ELEMENT)
@0
D=A
@THAT
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@1
D=A
@THAT
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
@2
D=A
@THAT
D=D+M
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@1
D=A
@THIS
A=D+A
D=M
@SP
M=M+1
A=M-1
M=D
@1
D=A
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
@THIS
D=D+A
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@0
D=A
@ARG
A=D+M
D=M
@SP
M=M+1
A=M-1
M=D
@1
D=A
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
@0
D=A
@ARG
D=D+M
@SP
A=M
M=D
@SP
M=M-1
A=M
D=M
A=A+1
A=M
M=D
@MAIN_LOOP_START
0;JMP
(END_PROGRAM)
