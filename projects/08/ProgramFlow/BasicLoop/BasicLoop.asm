(loop)
@0
D=A
@16
A=D+A
D=M
@SP
A=M
M=D
@SP
M=M+1
@BasicLoop.test$ret.0
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
D=D-1
@ARG
M=D
@SP
D=M
@LCL
M=D
@test
0;JMP
(BasicLoop.test$ret.0)
@0
D=A
@16
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
@loop
0;JMP
(test)
@SP
A=M
M=0
@SP
M=M+1
@5
D=A
@SP
A=M
M=D
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
