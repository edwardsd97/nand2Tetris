// This file is part of www.nand2tetris.org
// and the book "The Elements of Computing Systems"
// by Nisan and Schocken, MIT Press.
// File name: projects/04/Mult.asm

// Multiplies R0 and R1 and stores the result in R2.
// (R0, R1, R2 refer to RAM[0], RAM[1], and RAM[2], respectively.)
//
// This program only needs to handle arguments that satisfy
// R0 >= 0, R1 >= 0, and R0*R1 < 32768.

// R0 = 0;
// count = R0;
// if R0 == 0 || R1 == 0 goto end;
// for ( count = R0, count > 0; count-- )
//    R2 = R2 + R1;

// Init
	@R2
	M=0

// if R0 == 0 || R1 == 0 goto end;
	@R1
	D=M

	@END
	D;JEQ

	@R0
	D=M

	@END
	D;JEQ
	
// for ( count = R0, count > 0; count-- )

	// count = R0
	@count
	M=D

	(LOOP)
		// R2 = R2 + R1;
		@R1
		D=M
		@R2
		D=D+M
		M=D

		// count--
		@count
		D=M
		D=D-1
		M=D

		// count > 0
		@LOOP
		D;JGT

(END)
	@END
	0;JMP